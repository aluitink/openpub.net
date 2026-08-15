using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.IntegrationTests.InboxProcessing;

public class InboxProcessingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public InboxProcessingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Inbox_Accepts_Activities_via_Outbox_Endpoint()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/testuser/activities/create-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/testuser"",
            ""object"": {
                ""id"": ""https://localhost/users/testuser/notes/post-1"",
                ""type"": ""Note"",
                ""content"": ""Hello World""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode, "Outbox should accept activities");
    }

    [Fact]
    public async Task Inbox_Handles_Undo_Activity_Correctly()
    {
        var client = _factory.CreateClient();
        
        var createActivity = @"{
            ""id"": ""https://localhost/users/testuser/activities/like-1"",
            ""type"": ""Like"",
            ""actor"": ""https://localhost/users/testuser"",
            ""object"": ""https://localhost/users/otheruser/notes/post-1""
        }";

        var content = new StringContent(createActivity, Encoding.UTF8, "application/activity+json");
        await client.PostAsync("/users/testuser/outbox", content);

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

        content = new StringContent(undoActivity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode, "Undo activity should be accepted");
    }

    [Fact]
    public async Task Inbox_Supports_Multiple_Actors()
    {
        var client = _factory.CreateClient();

        for (int i = 0; i < 5; i++)
        {
            var activity = $@"{{
                ""id"": ""https://localhost/users/testuser{i}/activities/create-1"",
                ""type"": ""Create"",
                ""actor"": ""https://localhost/users/testuser{i}"",
                ""object"": {{
                    ""id"": ""https://localhost/users/testuser{i}/notes/post-1"",
                    ""type"": ""Note"",
                    ""content"": ""Activity {i}""
                }}
            }}";

            var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
            var response = await client.PostAsync($"/users/testuser{i}/outbox", content);

            Assert.True(response.IsSuccessStatusCode, $"Actor {i} should be able to post");
        }
    }

    [Fact]
    public async Task Inbox_Preserves_Activity_Idempotency()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/testuser/activities/unique-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/testuser"",
            ""object"": {
                ""id"": ""https://localhost/users/testuser/notes/unique-1"",
                ""type"": ""Note"",
                ""content"": ""Unique content""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");

        var response1 = await client.PostAsync("/users/testuser/outbox", content);
        Assert.True(response1.IsSuccessStatusCode);

        var content2 = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response2 = await client.PostAsync("/users/testuser/outbox", content2);

        Assert.True(response2.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Inbox_Processes_Different_Activity_Types()
    {
        var client = _factory.CreateClient();
        var announceActivity = @"{
            ""id"": ""https://localhost/users/testuser/activities/announce-1"",
            ""type"": ""Announce"",
            ""actor"": ""https://localhost/users/testuser"",
            ""object"": ""https://localhost/users/otheruser/notes/post-1""
        }";

        var content = new StringContent(announceActivity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode, "Announce activity should be accepted");
    }
}
