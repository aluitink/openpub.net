using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.IntegrationTests.MultiInstance;

public class AdditionalMultiInstanceTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AdditionalMultiInstanceTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MultiInstance_Post_And_Retrieve()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/multitest1/activities/create-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/multitest1"",
            ""object"": {
                ""id"": ""https://localhost/users/multitest1/notes/post-1"",
                ""type"": ""Note"",
                ""content"": ""Multi-instance test 1""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/multitest1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task MultiInstance_Different_Domains()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/multitest2/activities/create-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/multitest2"",
            ""object"": {
                ""id"": ""https://localhost/users/multitest2/notes/post-1"",
                ""type"": ""Note"",
                ""content"": ""Multi-instance test 2""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/multitest2/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task MultiInstance_Three_Actors()
    {
        var client = _factory.CreateClient();
        
        for (int i = 0; i < 3; i++)
        {
            var activity = $@"{{
                ""id"": ""https://localhost/users/multitest{i}/activities/create-1"",
                ""type"": ""Create"",
                ""actor"": ""https://localhost/users/multitest{i}"",
                ""object"": {{
                    ""id"": ""https://localhost/users/multitest{i}/notes/post-1"",
                    ""type"": ""Note"",
                    ""content"": ""Actor {i}""
                }}
            }}";

            var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
            var response = await client.PostAsync($"/users/multitest{i}/outbox", content);
            Assert.True(response.IsSuccessStatusCode, $"Actor {i} should succeed");
        }
    }

    [Fact]
    public async Task MultiInstance_Four_Actors()
    {
        var client = _factory.CreateClient();
        
        for (int i = 0; i < 4; i++)
        {
            var activity = $@"{{
                ""id"": ""https://localhost/users/multitest{i+3}/activities/create-1"",
                ""type"": ""Create"",
                ""actor"": ""https://localhost/users/multitest{i+3}"",
                ""object"": {{
                    ""id"": ""https://localhost/users/multitest{i+3}/notes/post-1"",
                    ""type"": ""Note"",
                    ""content"": ""Actor {i+3}""
                }}
            }}";

            var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
            var response = await client.PostAsync($"/users/multitest{i+3}/outbox", content);
            Assert.True(response.IsSuccessStatusCode, $"Actor {i+3} should succeed");
        }
    }

    [Fact]
    public async Task MultiInstance_Five_Actors()
    {
        var client = _factory.CreateClient();
        
        for (int i = 0; i < 5; i++)
        {
            var activity = $@"{{
                ""id"": ""https://localhost/users/multitest{i+7}/activities/create-1"",
                ""type"": ""Create"",
                ""actor"": ""https://localhost/users/multitest{i+7}"",
                ""object"": {{
                    ""id"": ""https://localhost/users/multitest{i+7}/notes/post-1"",
                    ""type"": ""Note"",
                    ""content"": ""Actor {i+7}""
                }}
            }}";

            var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
            var response = await client.PostAsync($"/users/multitest{i+7}/outbox", content);
            Assert.True(response.IsSuccessStatusCode, $"Actor {i+7} should succeed");
        }
    }

    [Fact]
    public async Task MultiInstance_Ten_Actors()
    {
        var client = _factory.CreateClient();
        
        for (int i = 0; i < 10; i++)
        {
            var activity = $@"{{
                ""id"": ""https://localhost/users/multitest{i+12}/activities/create-1"",
                ""type"": ""Create"",
                ""actor"": ""https://localhost/users/multitest{i+12}"",
                ""object"": {{
                    ""id"": ""https://localhost/users/multitest{i+12}/notes/post-1"",
                    ""type"": ""Note"",
                    ""content"": ""Actor {i+12}""
                }}
            }}";

            var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
            var response = await client.PostAsync($"/users/multitest{i+12}/outbox", content);
            Assert.True(response.IsSuccessStatusCode, $"Actor {i+12} should succeed");
        }
    }
}
