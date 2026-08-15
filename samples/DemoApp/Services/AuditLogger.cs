using System.Collections.Generic;
using System;
using System.Linq;
using System.Text.Json;

namespace DemoApp.Services;

public class AuditLogEntry
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? ActorId { get; set; }
    public string? ClientIP { get; set; }
    public string? Endpoint { get; set; }
    public string? Details { get; set; }
    public bool? Success { get; set; }
}

public class AuditLogger
{
    private readonly List<AuditLogEntry> _entries;
    private readonly object _lock = new();
    private const int MaxEntries = 10000;

    public AuditLogger()
    {
        _entries = new List<AuditLogEntry>();
    }

    public void Log(string eventType, string? actorId = null, string? clientIP = null, 
                   string? endpoint = null, string? details = null, bool? success = null)
    {
        var entry = new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            ActorId = actorId,
            ClientIP = clientIP,
            Endpoint = endpoint,
            Details = details,
            Success = success
        };

        lock (_lock)
        {
            _entries.Add(entry);

            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveRange(0, _entries.Count - MaxEntries);
            }
        }
    }

    public List<AuditLogEntry> GetEntries(string? eventType = null, DateTime? startDate = null, 
                                          DateTime? endDate = null, int limit = 100)
    {
        lock (_lock)
        {
            var query = _entries.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(eventType))
            {
                query = query.Where(e => e.EventType == eventType);
            }

            if (startDate.HasValue)
            {
                query = query.Where(e => e.Timestamp >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(e => e.Timestamp <= endDate.Value);
            }

            return query.OrderByDescending(e => e.Timestamp).Take(limit).ToList();
        }
    }

    public List<AuditLogEntry> GetRecentEntries(int limit = 100)
    {
        return GetEntries(null, null, null, limit);
    }

    public List<AuditLogEntry> GetLoginAttempts(int limit = 100)
    {
        return GetEntries("login_attempt", null, null, limit);
    }

    public List<AuditLogEntry> GetTokenEvents(int limit = 100)
    {
        return GetEntries("token_", null, null, limit);
    }

    public List<AuditLogEntry> GetRateLimitEvents(int limit = 100)
    {
        return GetEntries("rate_limit", null, null, limit);
    }

    public Dictionary<string, object> GetStatistics()
    {
        lock (_lock)
        {
            return new Dictionary<string, object>
            {
                { "totalEntries", _entries.Count },
                { "loginAttempts", _entries.Count(e => e.EventType == "login_attempt") },
                { "tokenEvents", _entries.Count(e => e.EventType.StartsWith("token_")) },
                { "rateLimitEvents", _entries.Count(e => e.EventType == "rate_limit") },
                { "securityEvents", _entries.Count(e => new[] { "login_attempt", "token_", "rate_limit" }
                    .Any(t => e.EventType == t || e.EventType.StartsWith(t))) }
            };
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }
}
