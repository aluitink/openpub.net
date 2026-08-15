using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.IntegrationTests.ActivityExchange;

public class ActivityExchangeTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ActivityExchangeTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Exchange_Create_With_Note()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/testuser/activities/create-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/testuser"",
            ""object"": {
                ""id"": ""https://localhost/users/testuser/notes/post-1"",
                ""type"": ""Note"",
                ""content"": ""Hello from ActivityExchangeTests""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Update_With_Note()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/testuser/activities/update-1"",
            ""type"": ""Update"",
            ""actor"": ""https://localhost/users/testuser"",
            ""object"": {
                ""id"": ""https://localhost/users/testuser/notes/post-1"",
                ""type"": ""Note"",
                ""content"": ""Updated content""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Delete_With_Note()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/testuser/activities/delete-1"",
            ""type"": ""Delete"",
            ""actor"": ""https://localhost/users/testuser"",
            ""object"": ""https://localhost/users/testuser/notes/post-1""
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Follow_Workflow()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/testuser/activities/follow-1"",
            ""type"": ""Follow"",
            ""actor"": ""https://localhost/users/testuser"",
            ""object"": ""https://localhost/users/otheruser""
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Announce_Workflow()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/testuser/activities/announce-1"",
            ""type"": ""Announce"",
            ""actor"": ""https://localhost/users/testuser"",
            ""object"": ""https://localhost/users/otheruser/notes/post-1""
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Like_Workflow()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/testuser/activities/like-1"",
            ""type"": ""Like"",
            ""actor"": ""https://localhost/users/testuser"",
            ""object"": ""https://localhost/users/otheruser/notes/post-1""
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Block_Workflow()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/testuser/activities/block-1"",
            ""type"": ""Block"",
            ""actor"": ""https://localhost/users/testuser"",
            ""object"": ""https://localhost/users/otheruser""
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Undo_Like()
    {
        var client = _factory.CreateClient();
        var undoActivity = @"{
            ""id"": ""https://localhost/users/testuser/activities/undo-1"",
            ""type"": ""Undo"",
            ""actor"": ""https://localhost/users/testuser"",
            ""object"": {
                ""id"": ""https://localhost/users/testuser/activities/like-1"",
                ""type"": ""Like"",
                ""actor"": ""https://localhost/users/testuser"",
                ""object"": ""https://localhost/users/otheruser/notes/post-1""
            }
        }";

        var content = new StringContent(undoActivity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Accept_Follow()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/testuser/activities/accept-1"",
            ""type"": ""Accept"",
            ""actor"": ""https://localhost/users/testuser"",
            ""object"": {
                ""id"": ""https://localhost/users/testuser/activities/follow-1"",
                ""type"": ""Follow"",
                ""actor"": ""https://localhost/users/follower"",
                ""object"": ""https://localhost/users/testuser""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Reject_Follow()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/testuser/activities/reject-1"",
            ""type"": ""Reject"",
            ""actor"": ""https://localhost/users/testuser"",
            ""object"": {
                ""id"": ""https://localhost/users/testuser/activities/follow-1"",
                ""type"": ""Follow"",
                ""actor"": ""https://localhost/users/follower"",
                ""object"": ""https://localhost/users/testuser""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Send_With_To_Field()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/testuser/activities/create-2"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/testuser"",
            ""to"": [""https://localhost/users/otheruser""],
            ""object"": {
                ""id"": ""https://localhost/users/testuser/notes/post-2"",
                ""type"": ""Note"",
                ""content"": ""To field test""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Send_With_Cc_Field()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/testuser/activities/create-3"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/testuser"",
            ""cc"": [""https://localhost/users/follower""],
            ""object"": {
                ""id"": ""https://localhost/users/testuser/notes/post-3"",
                ""type"": ""Note"",
                ""content"": ""Cc field test""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Send_With_Bcc_Field()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/testuser/activities/create-4"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/testuser"",
            ""bcc"": [""https://localhost/users/private-follower""],
            ""object"": {
                ""id"": ""https://localhost/users/testuser/notes/post-4"",
                ""type"": ""Note"",
                ""content"": ""Bcc field test""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Send_With_Tag_Field()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/testuser/activities/create-5"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/testuser"",
            ""tag"": [{""type"": ""Mention"", ""href"": ""https://localhost/users/mentioned-user"", ""name"": ""@mentioned-user""}],
            ""object"": {
                ""id"": ""https://localhost/users/testuser/notes/post-5"",
                ""type"": ""Note"",
                ""content"": ""Tagged content""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Exchange_Send_With_Attributed_To_Field()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/testuser/activities/create-6"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/testuser"",
            ""attributedTo"": ""https://localhost/users/testuser"",
            ""object"": {
                ""id"": ""https://localhost/users/testuser/notes/post-6"",
                ""type"": ""Note"",
                ""content"": ""Attributed to test""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode);
    }
}
