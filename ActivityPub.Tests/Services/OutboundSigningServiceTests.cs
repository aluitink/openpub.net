using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ActivityPub.Core.Models;
using ActivityPub.Core.Services;
using Xunit;

namespace ActivityPub.Tests.Services;

public class OutboundSigningServiceTests
{
    private readonly HttpClient _httpClient;

    public OutboundSigningServiceTests()
    {
        _httpClient = new HttpClient();
    }

    [Fact]
    public async Task SignActivityAsync_SignsActivityCorrectly()
    {
        var service = new OutboundSigningService(_httpClient);
        
        var actor = new Actor { Id = "https://example.com/users/test", Type = "Person" };
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Create",
            Actor = actor,
            Object = "https://example.com/objects/456",
            Published = DateTime.UtcNow
        };
        
        var recipient = "https://remote-server.com/inbox";
        
        var (signatureHeader, signedContent) = await service.SignActivityAsync(activity, recipient);
        
        Assert.NotNull(signatureHeader);
        Assert.NotNull(signedContent);
        Assert.Contains("Signature keyId=", signatureHeader);
        Assert.Contains("algorithm=\"rsa-sha256\"", signatureHeader);
        Assert.Contains("headers=\"", signatureHeader);
        Assert.Contains("signature=\"", signatureHeader);
        
        var deserializedActivity = JsonSerializer.Deserialize<Activity>(signedContent);
        Assert.NotNull(deserializedActivity);
        Assert.Equal(activity.Id, deserializedActivity.Id);
        Assert.Equal(activity.Type, deserializedActivity.Type);
    }

    [Fact]
    public async Task SignActivityAsync_ReturnsDifferentSignatureEachTime()
    {
        var service = new OutboundSigningService(_httpClient);
        
        var actor = new Actor { Id = "https://example.com/users/test", Type = "Person" };
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Create",
            Actor = actor,
            Object = "https://example.com/objects/456",
            Published = DateTime.UtcNow
        };
        
        var recipient = "https://remote-server.com/inbox";
        
        var (signatureHeader1, _) = await service.SignActivityAsync(activity, recipient);
        var (signatureHeader2, _) = await service.SignActivityAsync(activity, recipient);
        
        Assert.NotNull(signatureHeader1);
        Assert.NotNull(signatureHeader2);
        Assert.NotEqual(signatureHeader1, signatureHeader2);
    }

    [Fact]
    public async Task SignActivityAsync_WithDifferentActivities()
    {
        var service = new OutboundSigningService(_httpClient);
        
        var actor = new Actor { Id = "https://example.com/users/test", Type = "Person" };
        var activity1 = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Create",
            Actor = actor,
            Object = "https://example.com/objects/456"
        };
        
        var activity2 = new Activity
        {
            Id = "https://example.com/activities/789",
            Type = "Like",
            Actor = actor,
            Object = "https://example.com/objects/456"
        };
        
        var recipient = "https://remote-server.com/inbox";
        
        var (sig1, content1) = await service.SignActivityAsync(activity1, recipient);
        var (sig2, content2) = await service.SignActivityAsync(activity2, recipient);
        
        Assert.NotNull(sig1);
        Assert.NotNull(sig2);
        Assert.NotEqual(sig1, sig2);
        Assert.NotEqual(content1, content2);
    }

    [Fact]
    public async Task SignActivityAsync_WithEmptyActivityId()
    {
        var service = new OutboundSigningService(_httpClient);
        
        var actor = new Actor { Id = "https://example.com/users/test", Type = "Person" };
        var activity = new Activity
        {
            Id = "",
            Type = "Create",
            Actor = actor,
            Object = "https://example.com/objects/456"
        };
        
        var recipient = "https://remote-server.com/inbox";
        
        var (signatureHeader, signedContent) = await service.SignActivityAsync(activity, recipient);
        
        Assert.NotNull(signatureHeader);
        Assert.NotNull(signedContent);
    }

    [Fact]
    public async Task SignActivityAsync_ReturnsValidJson()
    {
        var service = new OutboundSigningService(_httpClient);
        
        var actor = new Actor { Id = "https://example.com/users/test", Type = "Person" };
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Create",
            Actor = actor,
            Object = "https://example.com/objects/456"
        };
        
        var recipient = "https://remote-server.com/inbox";
        
        var (_, signedContent) = await service.SignActivityAsync(activity, recipient);
        
        var deserialized = JsonSerializer.Deserialize<Activity>(signedContent);
        Assert.NotNull(deserialized);
        Assert.Equal("Create", deserialized.Type);
    }
}
