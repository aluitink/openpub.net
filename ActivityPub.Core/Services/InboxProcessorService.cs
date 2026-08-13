using ActivityPub.Core.Models;
using ActivityPub.Core.Interfaces;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using ActivityPub.Core.Infrastructure.Telemetry;
using System.Diagnostics;
using ActivityPub.Core.Events;

namespace ActivityPub.Core.Services;

/// <summary>
/// Inbox processor for handling incoming activities with resilient background processing
/// </summary>
public class InboxProcessorService : IDisposable
{
    private readonly Channel<ActivityPub.Core.Models.Activity> _inboxChannel;
    private readonly IActivityPubRepository _repository;
    private readonly ActivityPubEventDispatcher _eventDispatcher;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ILogger<InboxProcessorService> _logger;
    private readonly ActivityPubTelemetry _telemetry;

    public InboxProcessorService(
        IActivityPubRepository repository,
        ActivityPubEventDispatcher eventDispatcher,
        ILogger<InboxProcessorService> logger,
        ActivityPubTelemetry telemetry)
    {
        _repository = repository;
        _eventDispatcher = eventDispatcher;
        _logger = logger;
        _telemetry = telemetry;
        _inboxChannel = Channel.CreateBounded<ActivityPub.Core.Models.Activity>(new BoundedChannelOptions(1000));
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Start background processing
        Task.Run(ProcessInboxItemsAsync);
    }

    /// <summary>
    /// Adds an activity to the inbox for processing
    /// </summary>
    /// <param name="activity">The activity to process</param>
    public async Task AddToInboxAsync(ActivityPub.Core.Models.Activity activity)
    {
        await _inboxChannel.Writer.WriteAsync(activity);
    }

private async Task ProcessInboxItemsAsync()
{
    await foreach (var activity in _inboxChannel.Reader.ReadAllAsync(_cancellationTokenSource.Token))
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Processing inbox activity: {ActivityId}", activity.Id);
            
            // Process the activity (this is a simplified implementation)
            // In a real implementation, this would:
            // 1. Validate the activity
            // 2. Apply any business logic
            // 3. Store the activity
            // 4. Dispatch events
            
            // For now, we'll just store it
            var actorId = GetActorId(activity.Actor);
            if (!string.IsNullOrEmpty(actorId))
            {
                await _repository.SaveUserActorAsync(new Actor
                {
                    Id = actorId,
                    Type = "Person",
                    PreferredUsername = "temp-user",
                    Inbox = actorId + "/inbox",
                    Outbox = actorId + "/outbox",
                    Followers = actorId + "/followers",
                    Following = actorId + "/following",
                    Liked = actorId + "/liked",
                    Published = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                });
            }
            
            // Dispatch event
            var eventObj = new ActivityReceivedEvent(activity);
            await _eventDispatcher.DispatchAsync(eventObj);
            
            _logger.LogInformation("Successfully processed inbox activity: {ActivityId}", activity.Id);
            _telemetry.RecordActivityProcessed("InboxProcessActivity");
            _telemetry.RecordHttpRequestProcessed("POST", "/inbox", 200, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            // Log error - in a production system, this would be more robust
            // For now, we'll continue processing other activities
            _logger.LogError(ex, "Error processing inbox activity: {ActivityId}", activity.Id);
            _telemetry.RecordActivityError("InboxProcessActivity", ex);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogDebug("Completed processing inbox activity: {ActivityId} in {ElapsedMilliseconds} ms", activity.Id, stopwatch.ElapsedMilliseconds);
        }
    }
}

    private string? GetActorId(object? actor)
    {
        return actor switch
        {
            string id => id,
            Actor a => a.Id,
            _ => null
        };
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _inboxChannel.Writer.Complete();
    }
}