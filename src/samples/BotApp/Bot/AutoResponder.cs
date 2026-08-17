using ActivityPub.Core.Models;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Logging;

namespace BotApp.Bot;

public class AutoResponder
{
    private readonly ActivityPubOptions _options;
    private readonly ILogger<AutoResponder> _logger;
    private readonly HashSet<string> _processedActivities = new();
    private readonly object _lock = new();

    public AutoResponder(ActivityPubOptions options, ILogger<AutoResponder> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task HandleActivityAsync(Activity activity)
    {
        lock (_lock)
        {
            if (activity.Id == null || !_processedActivities.Add(activity.Id))
            {
                return;
            }
        }

        _logger.LogInformation("Processing activity: {ActivityId} of type: {ActivityType}", activity.Id, activity.Type);

        try
        {
            switch (activity.Type)
            {
                case "Follow":
                    await HandleFollowAsync(activity);
                    break;
                case "Create":
                    await HandleCreateAsync(activity);
                    break;
                case "Like":
                    await HandleLikeAsync(activity);
                    break;
                case "Announce":
                    await HandleAnnounceAsync(activity);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling activity: {ActivityId}", activity.Id);
        }
    }

    private async Task HandleFollowAsync(Activity activity)
    {
        var actor = activity.Actor as Actor ?? new Actor { Id = activity.ActorId };
        var actorId = actor.Id ?? activity.ActorId;

        if (string.IsNullOrEmpty(actorId))
        {
            _logger.LogWarning("Follow activity missing actor ID: {ActivityId}", activity.Id);
            return;
        }

        _logger.LogInformation("Auto-accepting follow from: {ActorId}", actorId);

        var acceptActivity = new Activity
        {
            Id = $"{_options.Domain}/activitypub/accept/{Guid.NewGuid():N}",
            Type = "Accept",
            Actor = $"{_options.Domain}/users/bot",
            Object = activity.Id,
            To = new[] { actorId }
        };

        await Task.CompletedTask;
    }

    private async Task HandleCreateAsync(Activity activity)
    {
        var createObj = activity.Object as Note ?? (activity.Object as Activity)?.Object as Note;
        var actor = activity.Actor as Actor ?? new Actor { Id = activity.ActorId };
        var actorId = actor.Id ?? activity.ActorId;

        if (createObj == null || string.IsNullOrEmpty(actorId))
        {
            return;
        }

        var content = createObj.Content ?? string.Empty;

        if (ShouldReplyToContent(content))
        {
            _logger.LogInformation("Auto-responding to mention from: {ActorId}", actorId);

            var replyContent = GenerateReply(content);
            var replyNote = new Note
            {
                Id = $"{_options.Domain}/activitypub/note/{Guid.NewGuid():N}",
                Type = "Note",
                Content = replyContent,
                InReplyTo = createObj.Id,
                To = new[] { actorId }
            };

            var replyActivity = new Create
            {
                Id = $"{_options.Domain}/activitypub/create/{Guid.NewGuid():N}",
                Type = "Create",
                Actor = $"{_options.Domain}/users/bot",
                Object = replyNote,
                To = new[] { actorId }
            };
        }
    }

    private async Task HandleLikeAsync(Activity activity)
    {
        _logger.LogInformation("Received like from: {ActorId}", activity.ActorId);
        await Task.CompletedTask;
    }

    private async Task HandleAnnounceAsync(Activity activity)
    {
        _logger.LogInformation("Received announce from: {ActorId}", activity.ActorId);
        await Task.CompletedTask;
    }

    private bool ShouldReplyToContent(string content)
    {
        var lowerContent = content.ToLowerInvariant();
        return lowerContent.Contains($"@bot@{_options.Domain}") ||
               lowerContent.Contains($"@bot@{_options.Domain}/") ||
               lowerContent.Contains($"@bot");
    }

    private string GenerateReply(string originalContent)
    {
        var random = new Random();
        var replies = new[]
        {
            "Thank you for your post!",
            "Great thoughts! 🤖",
            "I'm just a bot, but I agree!",
            "Thanks for the mention!",
            "Interesting perspective!"
        };

        return replies[random.Next(replies.Length)];
    }

    public void MarkActivityAsProcessed(string activityId)
    {
        lock (_lock)
        {
            _processedActivities.Add(activityId);
        }
    }

    public bool IsActivityProcessed(string activityId)
    {
        lock (_lock)
        {
            return _processedActivities.Contains(activityId);
        }
    }

    public void ClearProcessedCache()
    {
        lock (_lock)
        {
            _processedActivities.Clear();
        }
    }
}
