using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.IntegrationTests.WebFingerResolution;

public class AdditionalWebFingerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AdditionalWebFingerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task WebFinger_With_Underscore_User_Returns_Success()
    {
        var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:test_user@localhost");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task WebFinger_With_Hyphen_User_Returns_Success()
    {
        var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:test-user@localhost");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task WebFinger_With_Long_Username_Returns_Success()
    {
        var longUser = new string('a', 50);
        var response = await _client.GetAsync($"/.well-known/webfinger?resource=acct:{longUser}@localhost");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task WebFinger_With_Digits_Only_Username_Returns_Success()
    {
        var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:12345@localhost");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task WebFinger_Host_Document_Returns_Success()
    {
        var response = await _client.GetAsync("/.well-known/webfinger?resource=https://example.com");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task WebFinger_Missing_Resource_Returns_400()
    {
        var response = await _client.GetAsync("/.well-known/webfinger");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WebFinger_Whitespace_Only_Resource_Returns_400()
    {
        var response = await _client.GetAsync("/.well-known/webfinger?resource=%20%20%20");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WebFinger_Returns_Valid_Json()
    {
        var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:testuser@localhost");
        Assert.True(response.IsSuccessStatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonDocument>(content);
        Assert.NotNull(doc);
    }

    [Fact]
    public async Task WebFinger_Contains_Required_Links_Field()
    {
        var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:linkuser@localhost");
        Assert.True(response.IsSuccessStatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("links", content);
    }

    [Fact]
    public async Task WebFinger_Contains_Required_Subject_Field()
    {
        var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:subjuser@localhost");
        Assert.True(response.IsSuccessStatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("subject", content);
    }

    [Fact]
    public async Task WebFinger_Concurrent_Requests_Succeed()
    {
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_client.GetAsync($"/.well-known/webfinger?resource=acct:concurrent{i}@localhost"));
        }

        var results = await Task.WhenAll(tasks);
        foreach (var r in results)
            Assert.True(r.IsSuccessStatusCode);
    }

    [Fact]
    public async Task WebFinger_Repeated_Requests_Return_Consistent_Results()
    {
        var first = await _client.GetAsync("/.well-known/webfinger?resource=acct:repeatuser@localhost");
        var second = await _client.GetAsync("/.well-known/webfinger?resource=acct:repeatuser@localhost");

        var c1 = await first.Content.ReadAsStringAsync();
        var c2 = await second.Content.ReadAsStringAsync();

        Assert.NotEmpty(c1);
        Assert.NotEmpty(c2);
    }

    [Fact]
    public async Task WebFinger_Case_Insensitive_Host_Returns_Success()
    {
        var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:user@LOCALHOST");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task WebFinger_Mixed_Case_User_Returns_Success()
    {
        var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:TestUser@localhost");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task WebFinger_Single_Char_Username_Returns_Success()
    {
        var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:a@localhost");
        Assert.True(response.IsSuccessStatusCode);
    }
}
