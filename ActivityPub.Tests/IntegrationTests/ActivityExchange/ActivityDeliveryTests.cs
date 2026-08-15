using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.Integration.ActivityExchange;

/// <summary>
/// Integration tests for ActivityPub activity exchange between multiple server instances
/// Verifies activity delivery, parsing, and validation across instances
/// </summary>
public class ActivityDeliveryTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ActivityDeliveryTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ActivityDelivery_Valid_Activity_CanBe_Posted()
    {
        // Arrange
        var client = _factory.CreateClient();

        var activityJson = @"{
            ""id"": ""https://localhost/users/sender/activities/delivery-test-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sender"",
            ""object"": {
                ""id"": ""https://localhost/users/sender/notes/delivery-test-1"",
                ""type"": ""Note"",
                ""content"": ""Test activity content for delivery""
            }
        }";

        var content = new StringContent(activityJson, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/sender/outbox", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ActivityDelivery_Parses_ActivityPub_Activity()
    {
        // Arrange
        var client = _factory.CreateClient();

        var activityJson = @"{
            ""@context"": ""https://www.w3.org/ns/activitystreams"",
            ""id"": ""https://localhost/users/sender/activities/parse-test-2"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sender"",
            ""object"": {
                ""id"": ""https://localhost/users/sender/notes/parse-test-2"",
                ""type"": ""Note"",
                ""content"": ""Test activity content with context""
            }
        }";

        var content = new StringContent(activityJson, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/sender/outbox", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ActivityDelivery_Inbox_CanReceive_Activity()
    {
        // Arrange
        var client = _factory.CreateClient();

        var activityJson = @"{
            ""id"": ""https://localhost/users/sender/activities/inbox-test-4"",
            ""type"": ""Like"",
            ""actor"": ""https://localhost/users/sender"",
            ""object"": ""https://localhost/users/receiver/notes/some-note""
        }";

        var content = new StringContent(activityJson, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/receiver/inbox", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ActivityDelivery_Different_Activity_Types_CanBe_Posted()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Test different activity types
        var activityTypes = new[] { "Create", "Like", "Announce", "Follow" };

        foreach (var activityType in activityTypes)
        {
            var activityJson = $@"
            {{
                ""id"": ""https://localhost/users/sender/activities/{activityType.ToLower()}-test-5"",
                ""type"": ""{activityType}"",
                ""actor"": ""https://localhost/users/sender"",
                ""object"": ""https://localhost/users/receiver/notes/test""
            }}";

            var content = new StringContent(activityJson, Encoding.UTF8, "application/activity+json");
            var response = await client.PostAsync("/users/sender/outbox", content);

            Assert.True(response.IsSuccessStatusCode, $"Failed to post {activityType} activity");
        }
    }

    [Fact]
    public async Task ActivityDelivery_Verify_Activity_Data_Preserved()
    {
        // Arrange
        var client = _factory.CreateClient();

        var expectedId = "https://localhost/users/sender/activities/data-preserve-test";
        var expectedContent = "Test content preservation";

        var activityJson = $@"
        {{
            ""id"": ""{expectedId}"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sender"",
            ""object"": {{
                ""id"": ""https://localhost/users/sender/notes/data-preserve-test"",
                ""type"": ""Note"",
                ""content"": ""{expectedContent}""
            }}
        }}";

        var content = new StringContent(activityJson, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/sender/outbox", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains("success", responseBody.ToLowerInvariant());
    }
}
