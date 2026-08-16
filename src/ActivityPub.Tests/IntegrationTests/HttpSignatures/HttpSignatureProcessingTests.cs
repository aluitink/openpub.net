using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.IntegrationTests.HttpSignatures;

public class HttpSignatureProcessingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public HttpSignatureProcessingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SignatureValidation_Allows_Valid_Requests()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/testuser/activities/create-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/testuser"",
            ""object"": {
                ""id"": ""https://localhost/users/testuser/notes/post-1"",
                ""type"": ""Note"",
                ""content"": ""Test content""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode, "Valid requests should succeed");
    }

    [Fact]
    public async Task SignatureValidation_Supports_Patch_Requests()
    {
        var client = _factory.CreateClient();
        var patchActivity = @"{
            ""id"": ""https://localhost/users/testuser/activities/update-1"",
            ""type"": ""Update"",
            ""actor"": ""https://localhost/users/testuser"",
            ""object"": {
                ""id"": ""https://localhost/users/testuser/notes/post-1"",
                ""type"": ""Note"",
                ""content"": ""Updated content""
            }
        }";

        var content = new StringContent(patchActivity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode, "Update activities should be accepted");
    }

    [Fact]
    public async Task SignatureValidation_Processes_Multiple_Sequenced_Activities()
    {
        var client = _factory.CreateClient();

        for (int i = 1; i <= 5; i++)
        {
            var activity = $@"{{
                ""id"": ""https://localhost/users/testuser/activities/seq-{i}"",
                ""type"": ""Create"",
                ""actor"": ""https://localhost/users/testuser"",
                ""object"": {{
                    ""id"": ""https://localhost/users/testuser/notes/seq-{i}"",
                    ""type"": ""Note"",
                    ""content"": ""Sequential {i}""
                }}
            }}";

            var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
            var response = await client.PostAsync("/users/testuser/outbox", content);
            Assert.True(response.IsSuccessStatusCode, $"Activity {i} should succeed");
        }
    }
}
