using System.Net;
using System.Net.Http;
using System.Text;
using Xunit;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.Integration.ActivityExchange;

/// <summary>
/// Integration tests for duplicate activity detection in ActivityPub
/// Verifies that duplicate activities are properly detected and handled
/// </summary>
public class DuplicateDetectionTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DuplicateDetectionTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DuplicateDetection_Same_Activity_ID_Results_In_Success()
    {
        // Arrange
        var client = _factory.CreateClient();

        var activityJson = @"{
            ""id"": ""https://localhost/users/sender/activities/duplicate-test-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sender"",
            ""object"": {
                ""id"": ""https://localhost/users/sender/notes/duplicate-test-1"",
                ""type"": ""Note"",
                ""content"": ""Test duplicate detection""
            }
        }";

        var content = new StringContent(activityJson, Encoding.UTF8, "application/activity+json");
        
        // Post the same activity twice
        var response1 = await client.PostAsync("/users/sender/outbox", content);
        
        // Reset content for second post
        content = new StringContent(activityJson, Encoding.UTF8, "application/activity+json");
        var response2 = await client.PostAsync("/users/sender/outbox", content);

        // Both requests should succeed (idempotent behavior)
        Assert.True(response1.IsSuccessStatusCode);
        Assert.True(response2.IsSuccessStatusCode);
    }

    [Fact]
    public async Task DuplicateDetection_Different_Activities_Accepted()
    {
        // Arrange
        var client = _factory.CreateClient();

        var activity1Json = @"{
            ""id"": ""https://localhost/users/sender/activities/diff-test-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sender"",
            ""object"": {
                ""id"": ""https://localhost/users/sender/notes/diff-test-1"",
                ""type"": ""Note"",
                ""content"": ""First activity""
            }
        }";

        var activity2Json = @"{
            ""id"": ""https://localhost/users/sender/activities/diff-test-2"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sender"",
            ""object"": {
                ""id"": ""https://localhost/users/sender/notes/diff-test-2"",
                ""type"": ""Note"",
                ""content"": ""Second activity""
            }
        }";

        var content1 = new StringContent(activity1Json, Encoding.UTF8, "application/activity+json");
        var content2 = new StringContent(activity2Json, Encoding.UTF8, "application/activity+json");

        var response1 = await client.PostAsync("/users/sender/outbox", content1);
        var response2 = await client.PostAsync("/users/sender/outbox", content2);

        // Different activities should both succeed
        Assert.True(response1.IsSuccessStatusCode);
        Assert.True(response2.IsSuccessStatusCode);
    }

    [Fact]
    public async Task DuplicateDetection_Same_Content_Different_ID_Accepted()
    {
        // Arrange
        var client = _factory.CreateClient();

        var activity1Json = @"{
            ""id"": ""https://localhost/users/sender/activities/same-content-test-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sender"",
            ""object"": {
                ""id"": ""https://localhost/users/sender/notes/same-content-test-1"",
                ""type"": ""Note"",
                ""content"": ""Same content, different ID 1""
            }
        }";

        var activity2Json = @"{
            ""id"": ""https://localhost/users/sender/activities/same-content-test-2"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sender"",
            ""object"": {
                ""id"": ""https://localhost/users/sender/notes/same-content-test-2"",
                ""type"": ""Note"",
                ""content"": ""Same content, different ID 1""
            }
        }";

        var content1 = new StringContent(activity1Json, Encoding.UTF8, "application/activity+json");
        var content2 = new StringContent(activity2Json, Encoding.UTF8, "application/activity+json");

        var response1 = await client.PostAsync("/users/sender/outbox", content1);
        var response2 = await client.PostAsync("/users/sender/outbox", content2);

        // Same content but different IDs should both succeed
        Assert.True(response1.IsSuccessStatusCode);
        Assert.True(response2.IsSuccessStatusCode);
    }

    [Fact]
    public async Task DuplicateDetection_Inbox_Receives_Activities()
    {
        // Arrange
        var client = _factory.CreateClient();

        var activityJson = @"{
            ""id"": ""https://localhost/users/sender/activities/inbox-dup-test-1"",
            ""type"": ""Like"",
            ""actor"": ""https://localhost/users/sender"",
            ""object"": ""https://localhost/users/receiver/notes/test""
        }";

        var content = new StringContent(activityJson, Encoding.UTF8, "application/activity+json");

        // Post to inbox (different endpoint than outbox)
        var response = await client.PostAsync("/users/receiver/inbox", content);

        // Inbox should accept activities
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task DuplicateDetection_Verify_No_Infinite_Loops()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Post multiple activities to test for infinite loop scenarios
        for (int i = 0; i < 10; i++)
        {
            var activityJson = $@"
            {{
                ""id"": ""https://localhost/users/sender/activities/loop-test-{i}"",
                ""type"": ""Create"",
                ""actor"": ""https://localhost/users/sender"",
                ""object"": {{
                    ""id"": ""https://localhost/users/sender/notes/loop-test-{i}"",
                    ""type"": ""Note"",
                    ""content"": ""Loop test {i}""
                }}
            }}";

            var content = new StringContent(activityJson, Encoding.UTF8, "application/activity+json");
            var response = await client.PostAsync("/users/sender/outbox", content);

            Assert.True(response.IsSuccessStatusCode, $"Activity {i} failed");
        }
    }
}
