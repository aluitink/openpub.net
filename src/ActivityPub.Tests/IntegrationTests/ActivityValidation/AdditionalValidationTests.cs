using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.IntegrationTests.ActivityValidation;

public class AdditionalValidationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AdditionalValidationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Validation_Valid_Create_With_Embedded_Object()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/validate1/activities/create-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/validate1"",
            ""object"": {
                ""id"": ""https://localhost/users/validate1/notes/post-1"",
                ""type"": ""Note"",
                ""content"": ""Embedded object test""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/validate1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Validation_Valid_Update_With_Embedded_Object()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/validate1/activities/update-1"",
            ""type"": ""Update"",
            ""actor"": ""https://localhost/users/validate1"",
            ""object"": {
                ""id"": ""https://localhost/users/validate1/notes/post-1"",
                ""type"": ""Note"",
                ""content"": ""Updated embedded object""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/validate1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Validation_Valid_Delete_With_String_ID()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/validate1/activities/delete-1"",
            ""type"": ""Delete"",
            ""actor"": ""https://localhost/users/validate1"",
            ""object"": ""https://localhost/users/validate1/notes/post-1""
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/validate1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Validation_Valid_Follow_With_String_Object()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/validate1/activities/follow-1"",
            ""type"": ""Follow"",
            ""actor"": ""https://localhost/users/validate1"",
            ""object"": ""https://localhost/users/validate2""
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/validate1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Validation_Valid_Announce_With_String_Object()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/validate1/activities/announce-1"",
            ""type"": ""Announce"",
            ""actor"": ""https://localhost/users/validate1"",
            ""object"": ""https://localhost/users/validate2/notes/post-1""
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/validate1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Validation_Valid_Like_With_String_Object()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/validate1/activities/like-1"",
            ""type"": ""Like"",
            ""actor"": ""https://localhost/users/validate1"",
            ""object"": ""https://localhost/users/validate2/notes/post-1""
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/validate1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Validation_Valid_Block_With_String_Object()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/validate1/activities/block-1"",
            ""type"": ""Block"",
            ""actor"": ""https://localhost/users/validate1"",
            ""object"": ""https://localhost/users/validate2""
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/validate1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Validation_Valid_Undo_With_Object()
    {
        var client = _factory.CreateClient();
        var undoActivity = @"{
            ""id"": ""https://localhost/users/validate1/activities/undo-1"",
            ""type"": ""Undo"",
            ""actor"": ""https://localhost/users/validate1"",
            ""object"": {
                ""id"": ""https://localhost/users/validate1/activities/like-1"",
                ""type"": ""Like"",
                ""actor"": ""https://localhost/users/validate1"",
                ""object"": ""https://localhost/users/validate2/notes/post-1""
            }
        }";

        var content = new StringContent(undoActivity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/validate1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Validation_Valid_Accept_With_Object()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/validate1/activities/accept-1"",
            ""type"": ""Accept"",
            ""actor"": ""https://localhost/users/validate1"",
            ""object"": {
                ""id"": ""https://localhost/users/validate1/activities/follow-1"",
                ""type"": ""Follow"",
                ""actor"": ""https://localhost/users/validate2"",
                ""object"": ""https://localhost/users/validate1""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/validate1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Validation_Valid_Reject_With_Object()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/validate1/activities/reject-1"",
            ""type"": ""Reject"",
            ""actor"": ""https://localhost/users/validate1"",
            ""object"": {
                ""id"": ""https://localhost/users/validate1/activities/follow-1"",
                ""type"": ""Follow"",
                ""actor"": ""https://localhost/users/validate2"",
                ""object"": ""https://localhost/users/validate1""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/validate1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Validation_Valid_Send_With_To()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/validate1/activities/create-2"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/validate1"",
            ""to"": [""https://localhost/users/validate2""],
            ""object"": {
                ""id"": ""https://localhost/users/validate1/notes/post-2"",
                ""type"": ""Note"",
                ""content"": ""To field test""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/validate1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Validation_Valid_Send_With_Cc()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/validate1/activities/create-3"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/validate1"",
            ""cc"": [""https://localhost/users/validate3""],
            ""object"": {
                ""id"": ""https://localhost/users/validate1/notes/post-3"",
                ""type"": ""Note"",
                ""content"": ""Cc field test""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/validate1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Validation_Valid_Send_With_Bcc()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/validate1/activities/create-4"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/validate1"",
            ""bcc"": [""https://localhost/users/validate4""],
            ""object"": {
                ""id"": ""https://localhost/users/validate1/notes/post-4"",
                ""type"": ""Note"",
                ""content"": ""Bcc field test""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/validate1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Validation_Valid_Send_With_Tag()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/validate1/activities/create-5"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/validate1"",
            ""tag"": [{""type"": ""Mention"", ""href"": ""https://localhost/users/validate5"", ""name"": ""@validate5""}],
            ""object"": {
                ""id"": ""https://localhost/users/validate1/notes/post-5"",
                ""type"": ""Note"",
                ""content"": ""Tagged content""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/validate1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Validation_Valid_Send_With_AttributedTo()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/validate1/activities/create-6"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/validate1"",
            ""attributedTo"": ""https://localhost/users/validate1"",
            ""object"": {
                ""id"": ""https://localhost/users/validate1/notes/post-6"",
                ""type"": ""Note"",
                ""content"": ""Attributed to test""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/validate1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }
}
