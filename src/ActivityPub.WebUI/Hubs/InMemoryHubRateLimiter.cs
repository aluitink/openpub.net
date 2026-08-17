using System.Collections.Concurrent;

namespace ActivityPub.WebUI.Hubs;

/// <summary>
/// Process-local rate limiter. Each instance keeps its own in-memory counters.
/// This is the default for single-instance deployments where every connection
/// lives on exactly one process.
/// </summary>
public class InMemoryHubRateLimiter : IHubRateLimiter
{
    private sealed class State
    {
        public DateTime WindowStart { get; set; } = DateTime.MinValue;
        public int MessageCount { get; set; }
    }

    private readonly ConcurrentDictionary<string, State> _states = new();

    public Task<bool> TryRecordAsync(string connectionId, int maxMessages, TimeSpan window)
    {
        var state = _states.GetOrAdd(connectionId, _ => new State());
        var now = DateTime.UtcNow;

        lock (state)
        {
            if (now - state.WindowStart > window)
            {
                state.MessageCount = 0;
                state.WindowStart = now;
            }
            state.MessageCount++;
            return Task.FromResult(state.MessageCount <= maxMessages);
        }
    }

    public Task ClearAsync(string connectionId)
    {
        _states.TryRemove(connectionId, out _);
        return Task.CompletedTask;
    }
}
