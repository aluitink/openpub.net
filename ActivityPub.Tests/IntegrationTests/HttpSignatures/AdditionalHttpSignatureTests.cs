using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.IntegrationTests.HttpSignatures;

public class AdditionalHttpSignatureTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AdditionalHttpSignatureTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Signature_Request_With_Date_Header_Succeeds()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/sign1/activities/date-test-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sign1"",
            ""object"": {
                ""id"": ""https://localhost/users/sign1/notes/date-test-1"",
                ""type"": ""Note"",
                ""content"": ""Date header test""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/users/sign1/outbox") { Content = content };
        request.Headers.TryAddWithoutValidation("Date", DateTime.UtcNow.ToString("R"));

        var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Request_With_Digest_Header_Succeeds()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/sign1/activities/digest-test-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sign1"",
            ""object"": {
                ""id"": ""https://localhost/users/sign1/notes/digest-test-1"",
                ""type"": ""Note"",
                ""content"": ""Digest header test""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        content.Headers.TryAddWithoutValidation("Digest", "SHA-256=dGVzdA==");

        var response = await client.PostAsync("/users/sign1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Minimal_Activity_Succeeds()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/sign1/activities/minimal-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sign1"",
            ""object"": {
                ""id"": ""https://localhost/users/sign1/notes/minimal-1"",
                ""type"": ""Note"",
                ""content"": ""Minimal test""
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/sign1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Activity_With_Attributes_Succeeds()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/sign1/activities/attr-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sign1"",
            ""published"": ""2024-01-01T00:00:00Z"",
            ""to"": [""https://www.w3.org/ns/activitystreams#Public""],
            ""cc"": [""https://localhost/users/sign1/followers""],
            ""object"": {
                ""id"": ""https://localhost/users/sign1/notes/attr-1"",
                ""type"": ""Note"",
                ""content"": ""Attributes test"",
                ""attributedTo"": ""https://localhost/users/sign1"",
                ""inReplyTo"": null,
                ""sensitive"": false
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/sign1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Activity_With_Emoji_Succeeds()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/sign1/activities/emoji-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sign1"",
            ""object"": {
                ""id"": ""https://localhost/users/sign1/notes/emoji-1"",
                ""type"": ""Note"",
                ""content"": ""Hello World - Emoji content test!""
            }
        }";
        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/sign1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_With_Request_Target_Header_Succeeds()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/sign1/activities/rt-test-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sign1"",
            ""object"": {
                ""id"": ""https://localhost/users/sign1/notes/rt-test-1"",
                ""type"": ""Note"",
                ""content"": ""Request target test""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/sign1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_With_Host_Header_Succeeds()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/sign1/activities/host-test-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sign1"",
            ""object"": {
                ""id"": ""https://localhost/users/sign1/notes/host-test-1"",
                ""type"": ""Note"",
                ""content"": ""Host header test""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/users/sign1/outbox") { Content = content };
        request.Headers.Host = "localhost";

        var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Request_With_User_Agent_Succeeds()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ActivityPub.NET/1.0");

        var activity = @"{
            ""id"": ""https://localhost/users/sign1/activities/ua-test-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sign1"",
            ""object"": {
                ""id"": ""https://localhost/users/sign1/notes/ua-test-1"",
                ""type"": ""Note"",
                ""content"": ""User agent test""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/sign1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Request_Without_Authorization_Succeeds()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/sign1/activities/noauth-test-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sign1"",
            ""object"": {
                ""id"": ""https://localhost/users/sign1/notes/noauth-test-1"",
                ""type"": ""Note"",
                ""content"": ""No auth test""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/sign1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_With_Forwarded_Header_Succeeds()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/sign1/activities/fwd-test-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sign1"",
            ""object"": {
                ""id"": ""https://localhost/users/sign1/notes/fwd-test-1"",
                ""type"": ""Note"",
                ""content"": ""Forwarded test""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/users/sign1/outbox") { Content = content };
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "127.0.0.1");

        var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_With_KeepAlive_Connection_Succeeds()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/sign1/activities/keepalive-test-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sign1"",
            ""object"": {
                ""id"": ""https://localhost/users/sign1/notes/keepalive-test-1"",
                ""type"": ""Note"",
                ""content"": ""KeepAlive test""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/sign1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_With_Transfer_Encoding_Succeeds()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/sign1/activities/te-test-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sign1"",
            ""object"": {
                ""id"": ""https://localhost/users/sign1/notes/te-test-1"",
                ""type"": ""Note"",
                ""content"": ""Transfer encoding test""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var response = await client.PostAsync("/users/sign1/outbox", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_With_If_None_Match_Succeeds()
    {
        var client = _factory.CreateClient();
        var activity = @"{
            ""id"": ""https://localhost/users/sign1/activities/inm-test-1"",
            ""type"": ""Create"",
            ""actor"": ""https://localhost/users/sign1"",
            ""object"": {
                ""id"": ""https://localhost/users/sign1/notes/inm-test-1"",
                ""type"": ""Note"",
                ""content"": ""IfNoneMatch test""
            }
        }";

        var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/users/sign1/outbox") { Content = content };
        request.Headers.TryAddWithoutValidation("If-None-Match", "W/\"test\"");

        var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Signature_Batched_Requests_All_Succeed()
    {
        var client = _factory.CreateClient();
        var tasks = new List<Task<HttpResponseMessage>>();

        for (int i = 0; i < 5; i++)
        {
            var activity = $@"{{
                ""id"": ""https://localhost/users/sign1/activities/batch-{i}"",
                ""type"": ""Create"",
                ""actor"": ""https://localhost/users/sign1"",
                ""object"": {{
                    ""id"": ""https://localhost/users/sign1/notes/batch-{i}"",
                    ""type"": ""Note"",
                    ""content"": ""Batch {i}""
                }}
            }}";

            var content = new StringContent(activity, Encoding.UTF8, "application/activity+json");
            tasks.Add(client.PostAsync("/users/sign1/outbox", content));
        }

        var results = await Task.WhenAll(tasks);
        foreach (var r in results)
        {
            Assert.True(r.IsSuccessStatusCode);
        }
    }

    [Fact]
    public async Task Signature_Varying_Content_Lengths_All_Succeed()
    {
        var client = _factory.CreateClient();

        var sizes = new[] { 100, 500, 1000, 5000, 10000 };
        foreach (var size in sizes)
        {
            var content = new string('x', size);
            var activity = $@"{{
                ""id"": ""https://localhost/users/sign1/activities/cl-{size}"",
                ""type"": ""Create"",
                ""actor"": ""https://localhost/users/sign1"",
                ""object"": {{
                    ""id"": ""https://localhost/users/sign1/notes/cl-{size}"",
                    ""type"": ""Note"",
                    ""content"": ""{content}""
                }}
            }}";

            var stringContent = new StringContent(activity, Encoding.UTF8, "application/activity+json");
            var response = await client.PostAsync("/users/sign1/outbox", stringContent);
            Assert.True(response.IsSuccessStatusCode, $"Failed for content length {size}");
        }
    }
}
