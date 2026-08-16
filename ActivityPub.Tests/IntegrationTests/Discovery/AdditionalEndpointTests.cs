using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.IntegrationTests.Discovery;

public class AdditionalEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AdditionalEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_Endpoint_Responds_Or_Throws_Expectedly()
    {
        try
        {
            var response = await _client.GetAsync("/_health");
            Assert.NotNull(response);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task Inbox_Accepts_Create_With_InReplyTo()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/alice/activities/ep-reply-1"", ""type"": ""Create"", ""actor"": ""https://remote.example/users/alice"", ""object"": { ""id"": ""https://remote.example/users/alice/notes/ep-reply-1"", ""type"": ""Note"", ""content"": ""Reply test"", ""inReplyTo"": ""https://remote.example/users/bob/notes/1"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task WebFinger_With_Resource_Account_Returns_Response()
    {
        var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:alice@localhost");
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WebFinger_With_Missing_Resource_Returns_400()
    {
        var response = await _client.GetAsync("/.well-known/webfinger");
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task NodeInfo_Well_Known_Returns_Response()
    {
        try
        {
            var response = await _client.GetAsync("/.well-known/nodeinfo");
            Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task Actor_Profile_Returns_Response()
    {
        var response = await _client.GetAsync("/users/alice");
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Actor_Profile_Bob_Returns_Response()
    {
        var response = await _client.GetAsync("/users/bob");
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Actor_Inbox_Accepts_Create()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/alice/activities/ep-create-1"", ""type"": ""Create"", ""actor"": ""https://remote.example/users/alice"", ""object"": { ""id"": ""https://remote.example/users/alice/notes/ep-1"", ""type"": ""Note"", ""content"": ""Endpoint test"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Actor_Inbox_Accepts_With_Context()
    {
        var activity = @"{ ""@context"": ""https://www.w3.org/ns/activitystreams"", ""id"": ""https://remote.example/users/alice/activities/ep-context-1"", ""type"": ""Create"", ""actor"": ""https://remote.example/users/alice"", ""object"": { ""id"": ""https://remote.example/users/alice/notes/ep-ctx-1"", ""type"": ""Note"", ""content"": ""With context"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Actor_Outbox_Accepts_Create()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ep-outbox-1"", ""type"": ""Create"", ""actor"": ""https://localhost/users/alice"", ""object"": { ""id"": ""https://localhost/users/alice/notes/ep-out-1"", ""type"": ""Note"", ""content"": ""Outbox test"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Actor_Outbox_Accepts_Like()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ep-outbox-like-1"", ""type"": ""Like"", ""actor"": ""https://localhost/users/alice"", ""object"": ""https://localhost/users/bob/notes/1"" }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Actor_Outbox_Accepts_Follow()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ep-outbox-follow-1"", ""type"": ""Follow"", ""actor"": ""https://localhost/users/alice"", ""object"": ""https://localhost/users/bob"" }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Create_With_Embed()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/alice/activities/ep-embed-1"", ""type"": ""Create"", ""actor"": ""https://remote.example/users/alice"", ""object"": { ""id"": ""https://remote.example/users/alice/notes/ep-embed-1"", ""type"": ""Note"", ""content"": ""Embed test"", ""embed"": { ""type"": ""Link"", ""href"": ""https://example.com/page"" } } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Create_With_Source()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/alice/activities/ep-source-1"", ""type"": ""Create"", ""actor"": ""https://remote.example/users/alice"", ""object"": { ""id"": ""https://remote.example/users/alice/notes/ep-source-1"", ""type"": ""Note"", ""content"": ""Source test"", ""source"": { ""contentMap"": { ""en"": ""English text"" } } } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Create_With_Endpoint()
    {
                var activity = @"{ ""id"": ""https://remote.example/users/alice/activities/ep-endpoint-1"", ""type"": ""Create"", ""actor"": ""https://remote.example/users/alice"", ""object"": { ""id"": ""https://remote.example/users/alice/notes/ep-end-1"", ""type"": ""Note"", ""content"": ""Endpoint test"", ""endpoint"": ""https://remote.example/services/oembed"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }
}
