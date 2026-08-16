using System.Text.Json;
using ActivityPub.Core.Events;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActivityPub.Core.Implementations;

public class InboxProcessor : IActivityPubEventHandler
{
    private readonly IActivityPubRepository _repository;
    private readonly ILogger<InboxProcessor> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public InboxProcessor(IActivityPubRepository repository, ILogger<InboxProcessor> logger)
    {
        _repository = repository;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task HandleEventAsync(ActivityPubEvent @event)
    {
        if (@event is not ActivityReceivedEvent receivedEvent)
        {
            return;
        }

        var activity = receivedEvent.Activity;

        if (activity == null)
        {
            _logger.LogWarning("Received null activity in event");
            return;
        }

        try
        {
            await ProcessActivityAsync(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process activity {ActivityId}", activity.Id);
            throw;
        }
    }

    private async Task ProcessActivityAsync(Activity activity)
    {
        _logger.LogInformation("Processing activity {ActivityId} of type {ActivityType}",
            activity.Id, activity.Type);

        if (string.IsNullOrEmpty(activity.Id))
        {
            throw new InvalidDataException("Activity must have an ID");
        }

        if (string.IsNullOrEmpty(activity.Type))
        {
            throw new InvalidDataException("Activity must have a type");
        }

        await ValidateActivityAsync(activity);

        var activityType = activity.Type.ToLowerInvariant();

        switch (activityType)
        {
            case "create":
                await ProcessCreateAsync(activity);
                break;
            case "follow":
                await ProcessFollowAsync(activity);
                break;
            case "like":
                await ProcessLikeAsync(activity);
                break;
            case "announce":
                await ProcessAnnounceAsync(activity);
                break;
            case "undo":
                await ProcessUndoAsync(activity);
                break;
            case "delete":
                await ProcessDeleteAsync(activity);
                break;
            case "update":
                await ProcessUpdateAsync(activity);
                break;
            case "move":
                await ProcessMoveAsync(activity);
                break;
            default:
                _logger.LogWarning("Unknown activity type: {ActivityType}", activity.Type);
                throw new InvalidDataException($"Unknown activity type: {activity.Type}");
        }

        await _repository.SaveActivityAsync(activity);
    }

    private async Task ValidateActivityAsync(Activity activity)
    {
        if (string.IsNullOrEmpty(activity.Id))
        {
            throw new InvalidDataException("Activity must have an ID");
        }

        if (string.IsNullOrEmpty(activity.Type))
        {
            throw new InvalidDataException("Activity must have a type");
        }

        if (activity.Actor == null && string.IsNullOrEmpty(activity.ActorId))
        {
            throw new InvalidDataException("Activity must have an actor or actor ID");
        }

        if (activity.Object == null && string.IsNullOrEmpty(activity.ObjectId))
        {
            throw new InvalidDataException("Activity must have an object or object ID");
        }
    }

    private async Task ProcessCreateAsync(Activity activity)
    {
        _logger.LogInformation("Processing Create activity");

        if (activity.Object is Note note)
        {
            _logger.LogInformation("Created note: {NoteId}", note.Id ?? "unknown");
        }
        else if (activity.Object is Models.Object obj)
        {
            _logger.LogInformation("Created object of type: {ObjectType}", obj.Type ?? "unknown");
        }
        else if (activity.Object != null)
        {
            _logger.LogInformation("Created unknown object type: {ObjectType}", activity.Object.GetType().Name);
        }
    }

    private async Task ProcessFollowAsync(Activity activity)
    {
        _logger.LogInformation("Processing Follow activity");

        var actorId = activity.ActorId ?? GetActorIdFromObject(activity.Actor);
        var objectId = activity.ObjectId ?? GetActorIdFromObject(activity.Object);

        _logger.LogInformation("Actor {ActorId} is following {ObjectId}", actorId, objectId);

        if (activity.Actor is Actor actor)
        {
            await _repository.SaveUserActorAsync(actor);
        }
    }

    private async Task ProcessLikeAsync(Activity activity)
    {
        _logger.LogInformation("Processing Like activity");

        var actorId = activity.ActorId ?? GetActorIdFromObject(activity.Actor);
        var objectId = activity.ObjectId ?? GetActorIdFromObject(activity.Object);

        _logger.LogInformation("Actor {ActorId} liked {ObjectId}", actorId, objectId);
    }

    private async Task ProcessAnnounceAsync(Activity activity)
    {
        _logger.LogInformation("Processing Announce activity");

        var actorId = activity.ActorId ?? GetActorIdFromObject(activity.Actor);
        var objectId = activity.ObjectId ?? GetActorIdFromObject(activity.Object);

        _logger.LogInformation("Actor {ActorId} announced {ObjectId}", actorId, objectId);
    }

    private async Task ProcessUndoAsync(Activity activity)
    {
        _logger.LogInformation("Processing Undo activity");

        var actorId = activity.ActorId ?? GetActorIdFromObject(activity.Actor);
        var objectId = activity.ObjectId ?? GetActorIdFromObject(activity.Object);

        _logger.LogInformation("Actor {ActorId} undone {ObjectId}", actorId, objectId);

        if (activity.Object is Activity childActivity)
        {
            if (childActivity.Type?.ToLowerInvariant() == "follow")
            {
                var followActorId = childActivity.ActorId ?? GetActorIdFromObject(childActivity.Actor);
                _logger.LogInformation("Undoing follow: {FollowActorId} -> {ObjectId}", followActorId, objectId);
            }
        }
    }

    private async Task ProcessDeleteAsync(Activity activity)
    {
        _logger.LogInformation("Processing Delete activity");

        var actorId = activity.ActorId ?? GetActorIdFromObject(activity.Actor);
        var objectId = activity.ObjectId ?? GetActorIdFromObject(activity.Object);

        _logger.LogInformation("Actor {ActorId} deleted {ObjectId}", actorId, objectId);

        if (activity.Object is Models.Object obj)
        {
            if (obj.Type?.ToLowerInvariant() == "tombstone")
            {
                _logger.LogInformation("Tombstone confirmed for {ObjectId}", objectId);
            }
        }

        if (!string.IsNullOrEmpty(objectId))
        {
            var originalActivity = await _repository.GetActivityAsync(objectId);
            if (originalActivity != null)
            {
                await _repository.DeleteActivityAsync(objectId);
                _logger.LogInformation("Deleted original activity {ActivityId}", objectId);
            }
        }
    }

    private async Task ProcessUpdateAsync(Activity activity)
    {
        _logger.LogInformation("Processing Update activity");

        var actorId = activity.ActorId ?? GetActorIdFromObject(activity.Actor);
        var objectId = activity.ObjectId ?? GetActorIdFromObject(activity.Object);

        _logger.LogInformation("Actor {ActorId} updated {ObjectId}", actorId, objectId);

        if (activity.Object is Models.Object obj)
        {
            if (!string.IsNullOrEmpty(obj.Updated?.ToString()))
            {
                _logger.LogInformation("Updated timestamp: {Updated}", obj.Updated);
            }
        }
    }

    private async Task ProcessMoveAsync(Activity activity)
    {
        _logger.LogInformation("Processing Move activity");

        var actorId = activity.ActorId ?? GetActorIdFromObject(activity.Actor);
        var objectId = activity.ObjectId ?? GetActorIdFromObject(activity.Object);

        var target = activity.AdditionalProperties?.TryGetValue("target", out var targetVal) == true ? targetVal?.ToString() : null;
        _logger.LogInformation("Actor {ActorId} moved {ObjectId} to {Target}", actorId, objectId, target ?? "unknown");

        if (activity.Object is Models.Object obj)
        {
            if (obj.Type?.ToLowerInvariant() == "tombstone")
            {
                _logger.LogInformation("Tombstone for moved object {ObjectId}", objectId);
            }
        }
    }

    private string? GetActorIdFromObject(object? actorObject)
    {
        return actorObject switch
        {
            string id => id,
            Actor actor => actor.Id,
            _ => null
        };
    }
}
