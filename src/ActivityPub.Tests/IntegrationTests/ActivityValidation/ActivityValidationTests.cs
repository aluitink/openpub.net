using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.IntegrationTests.ActivityValidation;

public class ActivityValidationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ActivityValidationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Validation_Accepts_Valid_Create_Activity()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/testuser/activities/create-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/testuser"",
            ""object"": {
                ""id"": ""https://localhost/users/testuser/notes/post-1"",
                ""type"": ""Note"",
                ""content"": ""Valid content""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode, "Valid Create activity should be accepted");
    }

    [Fact]
    public async Task Validation_Accepts_Valid_Undo_Activity()
    {
        var client = _factory.CreateClient();
        var activity = @"{
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

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode, "Valid Undo activity should be accepted");
    }

    [Fact]
    public async Task Validation_Accepts_Valid_Like_Activity()
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

        Assert.True(response.IsSuccessStatusCode, "Valid Like activity should be accepted");
    }

    [Fact]
    public async Task Validation_Accepts_Valid_Announce_Activity()
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

        Assert.True(response.IsSuccessStatusCode, "Valid Announce activity should be accepted");
    }

    [Fact]
    public async Task Validation_Accepts_Valid_Update_Activity()
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

        Assert.True(response.IsSuccessStatusCode, "Valid Update activity should be accepted");
    }
}
