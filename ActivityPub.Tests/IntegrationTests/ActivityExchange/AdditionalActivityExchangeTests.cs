using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.IntegrationTests.ActivityExchange;

public class AdditionalActivityExchangeTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AdditionalActivityExchangeTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Exchange_Add_Object_To_Collection()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-add-1"", ""type"": ""Add"", ""actor"": ""https://localhost/users/alice"", ""object"": { ""id"": ""https://localhost/users/alice/notes/ex-1"", ""type"": ""Note"", ""content"": ""Added note"" }, ""target"": ""https://localhost/users/alice/collection/1"" }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Remove_Object_From_Collection()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-remove-1"", ""type"": ""Remove"", ""actor"": ""https://localhost/users/alice"", ""object"": { ""id"": ""https://localhost/users/alice/notes/ex-rm-1"", ""type"": ""Note"", ""content"": ""Removed note"" }, ""target"": ""https://localhost/users/alice/collection/2"" }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Offer_Activity()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-offer-1"", ""type"": ""Offer"", ""actor"": ""https://localhost/users/alice"", ""object"": { ""id"": ""https://localhost/users/alice/notes/ex-offer-obj-1"", ""type"": ""Note"", ""content"": ""Offered note"" }, ""target"": ""https://localhost/users/bob"" }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Invite_Activity()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-invite-1"", ""type"": ""Invite"", ""actor"": ""https://localhost/users/alice"", ""object"": { ""id"": ""https://localhost/users/alice/events/ex-invite-obj-1"", ""type"": ""Event"", ""name"": ""Gathering"" }, ""target"": ""https://localhost/users/bob"" }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Flag_Activity()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-flag-1"", ""type"": ""Flag"", ""actor"": ""https://localhost/users/alice"", ""object"": { ""id"": ""https://localhost/users/bob/notes/ex-flag-obj-1"", ""type"": ""Note"", ""content"": ""Flagged content"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_View_Activity()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-view-1"", ""type"": ""View"", ""actor"": ""https://localhost/users/alice"", ""object"": { ""id"": ""https://localhost/users/bob/notes/ex-view-obj-1"", ""type"": ""Note"", ""content"": ""Viewed content"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Read_Activity()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-read-1"", ""type"": ""Read"", ""actor"": ""https://localhost/users/alice"", ""object"": { ""id"": ""https://localhost/users/bob/notes/ex-read-obj-1"", ""type"": ""Note"", ""content"": ""Read content"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Travel_Activity()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-travel-1"", ""type"": ""Travel"", ""actor"": ""https://localhost/users/alice"", ""object"": { ""id"": ""https://localhost/users/alice/notes/ex-travel-obj-1"", ""type"": ""Note"", ""content"": ""Travelling"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_TentativeAccept_Activity()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-tentative-1"", ""type"": ""TentativeAccept"", ""actor"": ""https://localhost/users/alice"", ""object"": { ""id"": ""https://localhost/users/bob/activities/ex-inv-obj-1"", ""type"": ""Invite"", ""actor"": ""https://localhost/users/bob"", ""object"": ""https://localhost/users/alice"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_TentativeReject_Activity()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-tent-rej-1"", ""type"": ""TentativeReject"", ""actor"": ""https://localhost/users/alice"", ""object"": { ""id"": ""https://localhost/users/bob/activities/ex-inv-obj-2"", ""type"": ""Invite"", ""actor"": ""https://localhost/users/bob"", ""object"": ""https://localhost/users/alice"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Create_Article()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-article-1"", ""type"": ""Create"", ""actor"": ""https://localhost/users/alice"", ""object"": { ""id"": ""https://localhost/users/alice/articles/ex-1"", ""type"": ""Article"", ""name"": ""Article Title"", ""content"": ""Article body content here"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Create_Event()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-event-1"", ""type"": ""Create"", ""actor"": ""https://localhost/users/alice"", ""object"": { ""id"": ""https://localhost/users/alice/events/ex-1"", ""type"": ""Event"", ""name"": ""Community Meetup"", ""location"": { ""type"": ""Place"", ""name"": ""Town Hall"" } } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Create_Page()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-page-1"", ""type"": ""Create"", ""actor"": ""https://localhost/users/alice"", ""object"": { ""id"": ""https://localhost/users/alice/pages/ex-1"", ""type"": ""Page"", ""name"": ""My Page"", ""content"": ""Page content"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Update_Article()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-upd-article-1"", ""type"": ""Update"", ""actor"": ""https://localhost/users/alice"", ""object"": { ""id"": ""https://localhost/users/alice/articles/ex-upd-1"", ""type"": ""Article"", ""name"": ""Updated Title"", ""content"": ""Updated body"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Delete_Article()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-del-article-1"", ""type"": ""Delete"", ""actor"": ""https://localhost/users/alice"", ""object"": { ""id"": ""https://localhost/users/alice/articles/ex-del-1"", ""type"": ""Tombstone"", ""attributeTo"": ""https://localhost/users/alice"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Create_With_Icon()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-icon-1"", ""type"": ""Create"", ""actor"": ""https://localhost/users/alice"", ""object"": { ""id"": ""https://localhost/users/alice/notes/ex-icon-obj-1"", ""type"": ""Note"", ""content"": ""With icon"", ""icon"": { ""type"": ""Image"", ""url"": ""https://example.com/icon.png"" } } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Create_With_Image_Attachment()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-img-att-1"", ""type"": ""Create"", ""actor"": ""https://localhost/users/alice"", ""object"": { ""id"": ""https://localhost/users/alice/notes/ex-img-att-obj-1"", ""type"": ""Note"", ""content"": ""With image"", ""attachment"": { ""type"": ""Image"", ""url"": ""https://example.com/photo.jpg"", ""mediaType"": ""image/jpeg"" } } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Like_With_Name()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-like-name-1"", ""type"": ""Like"", ""actor"": ""https://localhost/users/alice"", ""object"": ""https://localhost/users/bob/notes/ex-like-obj-1"", ""name"": ""Liked post"" }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Create_With_Published()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-pub-1"", ""type"": ""Create"", ""actor"": ""https://localhost/users/alice"", ""published"": ""2026-01-15T10:00:00Z"", ""object"": { ""id"": ""https://localhost/users/alice/notes/ex-pub-obj-1"", ""type"": ""Note"", ""content"": ""Published note"" } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Create_With_Multiple_Tags()
    {
        var activity = @"{ ""id"": ""https://localhost/users/alice/activities/ex-mtags-1"", ""type"": ""Create"", ""actor"": ""https://localhost/users/alice"", ""object"": { ""id"": ""https://localhost/users/alice/notes/ex-mtags-obj-1"", ""type"": ""Note"", ""content"": ""Multi-tagged note"", ""tag"": [{ ""type"": ""Hashtag"", ""name"": ""#test"" }, { ""type"": ""Hashtag"", ""name"": ""#federation"" }, { ""type"": ""Mention"", ""href"": ""https://localhost/users/bob"", ""name"": ""@bob"" }] } }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await _client.PostAsync("/users/alice/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }
}
