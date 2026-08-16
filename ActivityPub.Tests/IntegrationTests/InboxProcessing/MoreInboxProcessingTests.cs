using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.IntegrationTests.InboxProcessing;

public class MoreInboxProcessingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MoreInboxProcessingTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Inbox_Accepts_View_Activity()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/bob/activities/inbox-view-1"", ""type"": ""View"", ""actor"": ""https://remote.example/users/bob"", ""object"": { ""id"": ""https://localhost/users/alice/notes/inbox-view-obj-1"", ""type"": ""Note"", ""content"": ""Viewed via inbox"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Rejects_Add_Activity()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/bob/activities/inbox-add-1"", ""type"": ""Add"", ""actor"": ""https://remote.example/users/bob"", ""object"": { ""id"": ""https://remote.example/users/bob/notes/inbox-add-obj-1"", ""type"": ""Note"", ""content"": ""Added via inbox"" }, ""target"": ""https://localhost/users/alice/collection/1"" }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Rejects_Remove_Activity()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/bob/activities/inbox-rm-1"", ""type"": ""Remove"", ""actor"": ""https://remote.example/users/bob"", ""object"": { ""id"": ""https://remote.example/users/bob/notes/inbox-rm-obj-1"", ""type"": ""Note"", ""content"": ""Removed via inbox"" }, ""target"": ""https://localhost/users/alice/collection/2"" }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Rejects_Flag_Activity()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/bob/activities/inbox-flag-1"", ""type"": ""Flag"", ""actor"": ""https://remote.example/users/bob"", ""object"": { ""id"": ""https://localhost/users/alice/notes/inbox-flag-obj-1"", ""type"": ""Note"", ""content"": ""Flagged via inbox"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Rejects_Offer_Activity()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/bob/activities/inbox-offer-1"", ""type"": ""Offer"", ""actor"": ""https://remote.example/users/bob"", ""object"": { ""id"": ""https://remote.example/users/bob/notes/inbox-offer-obj-1"", ""type"": ""Note"", ""content"": ""Offered via inbox"" }, ""target"": ""https://localhost/users/alice"" }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Rejects_Invite_Activity()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/bob/activities/inbox-inv-1"", ""type"": ""Invite"", ""actor"": ""https://remote.example/users/bob"", ""object"": { ""id"": ""https://remote.example/users/bob/events/inbox-inv-obj-1"", ""type"": ""Event"", ""name"": ""Inbox Event"" }, ""target"": ""https://localhost/users/alice"" }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Rejects_Read_Activity()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/bob/activities/inbox-read-1"", ""type"": ""Read"", ""actor"": ""https://remote.example/users/bob"", ""object"": { ""id"": ""https://localhost/users/alice/notes/inbox-read-obj-1"", ""type"": ""Note"", ""content"": ""Read via inbox"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Rejects_Travel_Activity()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/bob/activities/inbox-travel-1"", ""type"": ""Travel"", ""actor"": ""https://remote.example/users/bob"", ""object"": { ""id"": ""https://remote.example/users/bob/notes/inbox-travel-obj-1"", ""type"": ""Note"", ""content"": ""Travelling"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Create_Article()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/bob/activities/inbox-article-1"", ""type"": ""Create"", ""actor"": ""https://remote.example/users/bob"", ""object"": { ""id"": ""https://remote.example/users/bob/articles/inbox-article-obj-1"", ""type"": ""Article"", ""name"": ""Remote Article"", ""content"": ""Article content from remote inbox"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Create_With_Mention_Tag()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/bob/activities/inbox-mention-1"", ""type"": ""Create"", ""actor"": ""https://remote.example/users/bob"", ""object"": { ""id"": ""https://remote.example/users/bob/notes/inbox-mention-obj-1"", ""type"": ""Note"", ""content"": ""Mentioning alice"", ""tag"": [{ ""type"": ""Mention"", ""href"": ""https://localhost/users/alice"", ""name"": ""@alice"" }] } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Update_With_Location()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/bob/activities/inbox-loc-1"", ""type"": ""Update"", ""actor"": ""https://remote.example/users/bob"", ""object"": { ""id"": ""https://remote.example/users/bob/notes/inbox-loc-obj-1"", ""type"": ""Note"", ""content"": ""Updated with location"", ""location"": { ""type"": ""Place"", ""name"": ""Remote Place"" } } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Rejects_TentativeAccept()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/bob/activities/inbox-tent-accept-1"", ""type"": ""TentativeAccept"", ""actor"": ""https://remote.example/users/bob"", ""object"": { ""id"": ""https://localhost/users/alice/activities/inbox-tent-inv-1"", ""type"": ""Invite"", ""actor"": ""https://localhost/users/alice"", ""object"": ""https://remote.example/users/bob"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Create_With_Endpoint()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/bob/activities/inbox-endpoint-1"", ""type"": ""Create"", ""actor"": ""https://remote.example/users/bob"", ""object"": { ""id"": ""https://remote.example/users/bob/notes/inbox-endpoint-obj-1"", ""type"": ""Note"", ""content"": ""Has endpoint"", ""endpoint"": ""https://remote.example/services/oembed"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Create_With_Generator()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/bob/activities/inbox-gen-1"", ""type"": ""Create"", ""actor"": ""https://remote.example/users/bob"", ""object"": { ""id"": ""https://remote.example/users/bob/notes/inbox-gen-obj-1"", ""type"": ""Note"", ""content"": ""Generated post"", ""generator"": { ""type"": ""Software"", ""name"": ""TestBot"" } } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Create_With_AttributedTo()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/bob/activities/inbox-attr-1"", ""type"": ""Create"", ""actor"": ""https://remote.example/users/bob"", ""attributedTo"": ""https://remote.example/users/bob"", ""object"": { ""id"": ""https://remote.example/users/bob/notes/inbox-attr-obj-1"", ""type"": ""Note"", ""content"": ""Attributed post"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Accepts_Delete_With_Tombstone_AttributeTo()
    {
        var activity = @"{ ""id"": ""https://remote.example/users/bob/activities/inbox-del-tomb-1"", ""type"": ""Delete"", ""actor"": ""https://remote.example/users/bob"", ""object"": { ""id"": ""https://remote.example/users/bob/notes/inbox-del-tomb-obj-1"", ""type"": ""Tombstone"", ""attributedTo"": ""https://remote.example/users/bob"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/inbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }
}
