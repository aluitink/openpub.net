using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BotApp.Bot;

public class RelayService : IHostedService
{
    private readonly ActivityPubOptions _options;
    private readonly IActivityPubRepository _repository;
    private readonly ILogger<RelayService> _logger;
    private readonly AutoResponder _autoResponder;
    private readonly CancellationTokenSource _cts = new();
    private Task? _relayTask;

    public RelayService(
        ActivityPubOptions options,
        IActivityPubRepository repository,
        ILogger<RelayService> logger,
        AutoResponder autoResponder)
    {
        _options = options;
        _repository = repository;
        _logger = logger;
        _autoResponder = autoResponder;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Relay service starting");

        _relayTask = Task.Run(() => ExecuteAsync(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    private async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await RelayActivitiesAsync(stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in relay loop");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Relay service error");
        }

        _logger.LogInformation("Relay service stopped");
    }

    private async Task RelayActivitiesAsync(CancellationToken cancellationToken)
    {
        var botActor = $"{_options.Domain}/users/bot";

        var followers = await _repository.GetFollowersAsync("bot", 0, 100);

        if (!followers.Any())
        {
            _logger.LogDebug("No followers to relay to");
            return;
        }

        var recentActivities = await GetRecentOutboxActivitiesAsync(botActor, 10);

        foreach (var activityId in recentActivities)
        {
            var activity = await _repository.GetActivityAsync(activityId);

            if (activity == null)
            {
                continue;
            }

            var activityJson = System.Text.Json.JsonSerializer.Serialize(activity);

            foreach (var follower in followers.Take(10))
            {
                try
                {
                    await RelayActivityToActorAsync(follower, activity, activityJson, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to relay activity to follower: {Follower}", follower);
                }
            }
        }
    }

    private async Task RelayActivityToActorAsync(string follower, Activity activity, string activityJson, CancellationToken cancellationToken)
    {
        var followerActor = await _repository.GetUserActorAsync(follower.Split('/').Last());

        if (followerActor == null)
        {
            return;
        }

        _logger.LogInformation("Relaying activity to: {Follower}", follower);

        var relayedActivity = new Activity
        {
            Id = $"{_options.Domain}/activitypub/relay/{Guid.NewGuid():N}",
            Type = "Announce",
            Actor = $"{_options.Domain}/users/bot",
            Object = activity.Id,
            To = new[] { follower }
        };

        await Task.CompletedTask;
    }

    private async Task<ICollection<string>> GetRecentOutboxActivitiesAsync(string actorId, int limit)
    {
        return await _repository.GetActorOutboxActivitiesAsync(actorId.Split('/').Last(), 0, limit);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        return Task.CompletedTask;
    }

    public async Task RelayToFollowersAsync(Activity activity)
    {
        var followers = await _repository.GetFollowersAsync("bot", 0, 100);

        if (!followers.Any())
        {
            _logger.LogDebug("No followers to relay to");
            return;
        }

        var activityJson = System.Text.Json.JsonSerializer.Serialize(activity);

        foreach (var follower in followers.Take(10))
        {
            try
            {
                await RelayActivityToActorAsync(follower, activity, activityJson, _cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to relay activity to follower: {Follower}", follower);
            }
        }
    }

    public void StartRelay()
    {
        _relayTask ??= Task.Run(() => ExecuteAsync(_cts.Token), _cts.Token);
    }

    public void StopRelay()
    {
        _cts.Cancel();
    }
}
