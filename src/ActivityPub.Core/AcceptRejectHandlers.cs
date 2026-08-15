using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActivityPub.Core;

/// <summary>
/// Handler for Accept activities (follow acceptance)
/// </summary>
public class AcceptActivityHandler : ActivityTypeHandlerBase
{
    public override string ActivityType => "Accept";

    public override async Task HandleAsync(
        Activity activity,
        IActivityPubRepository repository,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing Accept activity: {ActivityId}", activity.Id);

        var actorId = activity.ActorId;
        var objectId = activity.ObjectId;

        if (string.IsNullOrEmpty(actorId) || string.IsNullOrEmpty(objectId))
        {
            logger.LogWarning("Accept activity missing actor or object ID");
            return;
        }

        logger.LogInformation("Accept activity from {Actor} for {Object}", actorId, objectId);

        await repository.SaveActivityAsync(activity);

        logger.LogInformation("Successfully processed Accept activity: {ActivityId}", activity.Id);
    }
}

/// <summary>
/// Handler for Reject activities (follow rejection)
/// </summary>
public class RejectActivityHandler : ActivityTypeHandlerBase
{
    public override string ActivityType => "Reject";

    public override async Task HandleAsync(
        Activity activity,
        IActivityPubRepository repository,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing Reject activity: {ActivityId}", activity.Id);

        var actorId = activity.ActorId;
        var objectId = activity.ObjectId;

        if (string.IsNullOrEmpty(actorId) || string.IsNullOrEmpty(objectId))
        {
            logger.LogWarning("Reject activity missing actor or object ID");
            return;
        }

        logger.LogInformation("Reject activity from {Actor} for {Object}", actorId, objectId);

        await repository.SaveActivityAsync(activity);

        logger.LogInformation("Successfully processed Reject activity: {ActivityId}", activity.Id);
    }
}
