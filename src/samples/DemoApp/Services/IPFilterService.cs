using System.Collections.Generic;
using System;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;

namespace DemoApp.Services;

public class IPFilterOptions
{
    public bool Enabled { get; set; } = false;
    public List<string> Whitelist { get; set; } = new();
    public List<string> Blacklist { get; set; } = new();
}

public class IPFilterEntry
{
    public string IP { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

public class IPFilterService
{
    private readonly IMemoryCache _cache;
    private readonly object _lock = new();
    private readonly HashSet<string> _whitelist;
    private readonly HashSet<string> _blacklist;

    public IPFilterService(IMemoryCache cache)
    {
        _cache = cache;
        _whitelist = new HashSet<string>();
        _blacklist = new HashSet<string>();
        _cache.Set("ip_filter_service_initialized", true);
    }

    public bool IsIPAllowed(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return false;
        }

        if (_blacklist.Contains(ipAddress))
        {
            return false;
        }

        if (_whitelist.Count > 0 && !_whitelist.Contains(ipAddress))
        {
            return false;
        }

        return true;
    }

    public void AddToWhitelist(string ipAddress, string? reason = null)
    {
        lock (_lock)
        {
            _whitelist.Add(ipAddress);
            _cache.Set($"ip_whitelist_{ipAddress}", true);
            _cache.Set("ip_whitelist_all", _whitelist.ToList());
        }
    }

    public void RemoveFromWhitelist(string ipAddress)
    {
        lock (_lock)
        {
            _whitelist.Remove(ipAddress);
            _cache.Remove($"ip_whitelist_{ipAddress}");
            _cache.Set("ip_whitelist_all", _whitelist.ToList());
        }
    }

    public void AddToBlacklist(string ipAddress, string? reason = null)
    {
        lock (_lock)
        {
            _blacklist.Add(ipAddress);
            _cache.Set($"ip_blacklist_{ipAddress}", true);
            _cache.Set("ip_blacklist_all", _blacklist.ToList());
        }
    }

    public void RemoveFromBlacklist(string ipAddress)
    {
        lock (_lock)
        {
            _blacklist.Remove(ipAddress);
            _cache.Remove($"ip_blacklist_{ipAddress}");
            _cache.Set("ip_blacklist_all", _blacklist.ToList());
        }
    }

    public List<string> GetWhitelist()
    {
        return _whitelist.ToList();
    }

    public List<string> GetBlacklist()
    {
        return _blacklist.ToList();
    }

    public bool IsIPWhitelisted(string ipAddress)
    {
        return _whitelist.Contains(ipAddress);
    }

    public bool IsIPBlacklisted(string ipAddress)
    {
        return _blacklist.Contains(ipAddress);
    }
}
