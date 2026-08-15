using ActivityPub.Core.Models;
using ActivityPub.Core.Interfaces;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
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

    public InboxProcessorService(
        IActivityPubRepository repository,
        ActivityPubEventDispatcher eventDispatcher,
        ILogger<InboxProcessorService> logger)
    {
        _repository = repository;
        _eventDispatcher = eventDispatcher;
        _logger = logger;
        _inboxChannel = Channel.CreateBounded<ActivityPub.Core.Models.Activity>(new BoundedChannelOptions(1000));
        _cancellationTokenSource = new CancellationTokenSource();

        Task.Run(ProcessInboxItemsAsync);
    }

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

                var eventObj = new ActivityReceivedEvent(activity);
                await _eventDispatcher.DispatchAsync(eventObj);

                _logger.LogInformation("Successfully processed inbox activity: {ActivityId}", activity.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing inbox activity: {ActivityId}", activity.Id);
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
