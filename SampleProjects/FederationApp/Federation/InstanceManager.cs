using ActivityPub.Core;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using ActivityPub.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FederationApp.Federation;

public class InstanceManager
{
    private readonly ActivityPubDbContext _dbContext;

    public InstanceManager(ActivityPubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<InstanceInfo>> GetInstancesAsync()
    {
        return await _dbContext.Instances.Select(i => new InstanceInfo
        {
            InstanceId = i.Id.ToString(),
            Domain = i.Domain,
            ActorId = $"https://{i.Domain}/actor",
            InboxUrl = $"https://{i.Domain}/inbox",
            PublicKey = "",
            LastContacted = i.LastFetched,
            IsConnected = i.IsActive,
            SuccessfulDeliveries = 0,
            FailedDeliveries = 0
        }).ToListAsync();
    }

    public async Task AddInstanceAsync(InstanceInfo instance)
    {
        var dbInstance = new Instance
        {
            Domain = instance.Domain,
            LastFetched = instance.LastContacted,
            IsActive = instance.IsConnected,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.Instances.AddAsync(dbInstance);
        await _dbContext.SaveChangesAsync();
    }

    public async Task RemoveInstanceAsync(string domain)
    {
        var instance = await _dbContext.Instances.FirstOrDefaultAsync(i => i.Domain == domain);
        if (instance != null)
        {
            _dbContext.Instances.Remove(instance);
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<List<InstanceInfo>> GetConnectedInstancesAsync()
    {
        return await _dbContext.Instances.Where(i => i.IsActive).Select(i => new InstanceInfo
        {
            InstanceId = i.Id.ToString(),
            Domain = i.Domain,
            ActorId = $"https://{i.Domain}/actor",
            InboxUrl = $"https://{i.Domain}/inbox",
            PublicKey = "",
            LastContacted = i.LastFetched,
            IsConnected = i.IsActive,
            SuccessfulDeliveries = 0,
            FailedDeliveries = 0
        }).ToListAsync();
    }
}

public class Instance
{
    public int Id { get; set; }
    public string Domain { get; set; } = string.Empty;
    public DateTime LastFetched { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}
