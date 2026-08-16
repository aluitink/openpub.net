using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.IntegrationTests.InboxProcessing;

public class AdditionalInboxDeliveryTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AdditionalInboxDeliveryTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Inbox_Accepts_Create_Activity()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/alice/activities/inbox-create-1"", ""type"": ""Create"", ""actor"": ""https://remote.example/users/alice"", ""object"": { ""id"": ""https://remote.example/users/alice/notes/ic-1"", ""type"": ""Note"", ""content"": ""Inbox create test"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Update_Activity()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/alice/activities/inbox-update-1"", ""type"": ""Update"", ""actor"": ""https://remote.example/users/alice"", ""object"": { ""id"": ""https://remote.example/users/alice/notes/ic-1"", ""type"": ""Note"", ""content"": ""Inbox update test"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Delete_Activity()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/alice/activities/inbox-delete-1"", ""type"": ""Delete"", ""actor"": ""https://remote.example/users/alice"", ""object"": { ""id"": ""https://remote.example/users/alice/notes/ic-1"", ""type"": ""Tombstone"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Follow_Activity()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/alice/activities/inbox-follow-1"", ""type"": ""Follow"", ""actor"": ""https://remote.example/users/alice"", ""object"": ""https://localhost/users/bob"" }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/bob/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Like_Activity()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/alice/activities/inbox-like-1"", ""type"": ""Like"", ""actor"": ""https://remote.example/users/alice"", ""object"": ""https://localhost/users/bob/notes/5"" }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/bob/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Announce_Activity()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/alice/activities/inbox-announce-1"", ""type"": ""Announce"", ""actor"": ""https://remote.example/users/alice"", ""object"": ""https://localhost/users/bob/notes/5"" }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/bob/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Undo_Activity()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/alice/activities/inbox-undo-1"", ""type"": ""Undo"", ""actor"": ""https://remote.example/users/alice"", ""object"": { ""id"": ""https://remote.example/users/alice/activities/like-99"", ""type"": ""Like"", ""actor"": ""https://remote.example/users/alice"", ""object"": ""https://localhost/users/bob/notes/5"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/bob/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Accept_Activity()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/bob/activities/inbox-accept-1"", ""type"": ""Accept"", ""actor"": ""https://remote.example/users/bob"", ""object"": { ""id"": ""https://remote.example/users/alice/activities/follow-99"", ""type"": ""Follow"", ""actor"": ""https://remote.example/users/alice"", ""object"": ""https://localhost/users/bob"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Reject_Activity()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/bob/activities/inbox-reject-1"", ""type"": ""Reject"", ""actor"": ""https://remote.example/users/bob"", ""object"": { ""id"": ""https://remote.example/users/alice/activities/follow-98"", ""type"": ""Follow"", ""actor"": ""https://remote.example/users/alice"", ""object"": ""https://localhost/users/bob"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Processes_Sequential_Activities()
    {
        for (int i = 0; i < 10; i++)
        {
            var activity = $@"{{ ""id"": ""https://remote.example/users/alice/activities/inbox-seq-{i}"", ""type"": ""Create"", ""actor"": ""https://remote.example/users/alice"", ""object"": {{ ""id"": ""https://remote.example/users/alice/notes/seq-{i}"", ""type"": ""Note"", ""content"": ""Sequential {i}"" }} }}";
            var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
            var response = await _client.PostAsync("/users/alice/inbox", content);
            Assert.True(response.IsSuccessStatusCode, $"Activity {i} failed");
        }
    }

    [Fact]
    public async Task Inbox_Processes_Concurrent_Activities()
    {
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 10; i++)
        {
            var activity = $@"{{ ""id"": ""https://remote.example/users/alice/activities/inbox-par-{i}"", ""type"": ""Create"", ""actor"": ""https://remote.example/users/alice"", ""object"": {{ ""id"": ""https://remote.example/users/alice/notes/par-{i}"", ""type"": ""Note"", ""content"": ""Parallel {i}"" }} }}";
            var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
            tasks.Add(_client.PostAsync("/users/alice/inbox", content));
        }
        var results = await Task.WhenAll(tasks);
        foreach (var r in results)
            Assert.True(r.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Activity_With_Embed()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/alice/activities/inbox-embed-1"", ""type"": ""Create"", ""actor"": ""https://remote.example/users/alice"", ""object"": { ""id"": ""https://remote.example/users/alice/notes/embed-1"", ""type"": ""Note"", ""content"": ""Post with embed"", ""attachment"": { ""type"": ""Document"", ""url"": ""https://example.com/video.mp4"", ""mediaType"": ""video/mp4"" } } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Activity_With_Multiple_Tags()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/alice/activities/inbox-tags-1"", ""type"": ""Create"", ""actor"": ""https://remote.example/users/alice"", ""object"": { ""id"": ""https://remote.example/users/alice/notes/tags-1"", ""type"": ""Note"", ""content"": ""Multi tagged"", ""tag"": [{ ""type"": ""Hashtag"", ""name"": ""#test1"" }, { ""type"": ""Hashtag"", ""name"": ""#test2"" }] } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }
}
