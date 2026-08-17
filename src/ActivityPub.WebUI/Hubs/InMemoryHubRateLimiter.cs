using System.Collections.Concurrent;

namespace ActivityPub.WebUI.Hubs;

/// <summary>
/// Process-local rate limiter. Each instance keeps its own in-memory counters.
/// This is the default for single-instance deployments where every connection
/// lives on exactly one process.
/// </summary>
public class InMemoryHubRateLimiter : IHubRateLimiter
{
    // Amortize the idle-connection sweep across TryRecord calls. The normal
    // disconnect path removes a connection's state (ClearAsync from
    // OnDisconnectedAsync), but connections that drop abruptly (crash, network
    // reset) never clear; without a sweep their entries would accumulate for the
    // process lifetime.
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);

    // A connection state is evicted once idle (no messages) for this long. Two
    // times the largest typical window outlasts any in-flight window, so
    // evicting an idle state can never reset an active connection's counter.
    private static readonly TimeSpan IdleEviction = TimeSpan.FromMinutes(5);

    private sealed class State
    {
        public DateTime WindowStart { get; set; } = DateTime.MinValue;
        public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
        public int MessageCount { get; set; }
    }

    private readonly ConcurrentDictionary<string, State> _states = new();
    private DateTime _lastSweepUtc = DateTime.UtcNow;

    public Task<bool> TryRecordAsync(string connectionId, int maxMessages, TimeSpan window)
    {
        var now = DateTime.UtcNow;
        SweepIdle();

        var state = _states.GetOrAdd(connectionId, _ => new State { LastSeenUtc = now });

        lock (state)
        {
            if (now - state.WindowStart > window)
            {
                state.MessageCount = 0;
                state.WindowStart = now;
            }
            state.MessageCount++;
            state.LastSeenUtc = now;
            return Task.FromResult(state.MessageCount <= maxMessages);
        }
    }

    public Task ClearAsync(string connectionId)
    {
        _states.TryRemove(connectionId, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes connection states that have been idle (no messages) for longer
    /// than <see cref="IdleEviction"/>, bounding growth for connections that
    /// never cleanly disconnected. Called opportunistically at most once per
    /// <see cref="SweepInterval"/> from <see cref="TryRecordAsync"/>.
    /// </summary>
    private void SweepIdle()
    {
        var now = DateTime.UtcNow;
        if (now - _lastSweepUtc < SweepInterval)
            return;

        var lastSweep = _lastSweepUtc;
        _lastSweepUtc = now;

        foreach (var kvp in _states)
        {
            if (now - kvp.Value.LastSeenUtc >= IdleEviction)
            {
                _states.TryRemove(kvp.Key, out _);
            }
        }
    }
}
