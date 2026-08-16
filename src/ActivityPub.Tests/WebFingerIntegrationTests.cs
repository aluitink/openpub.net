using System.Net.Http;
using System.Text.Json;
using Xunit;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests;

public class WebFingerIntegrationTests
{
    private readonly TestWebApplicationFactory _factory = new();

    [Fact]
    public async Task WebFinger_Returns_Successful_Response()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/.well-known/webfinger?resource=acct:test@localhost");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task WebFinger_Returns_Valid_Json_Response()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/.well-known/webfinger?resource=acct:json@localhost");

        Assert.True(response.IsSuccessStatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jrd = JsonSerializer.Deserialize<WebFingerJrd>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(jrd);
        Assert.NotEmpty(jrd.Links);
    }

    [Fact]
    public async Task WebFinger_Cache_Improves_Subsequent_Requests()
    {
        var client = _factory.CreateClient();

        var firstResponse = await client.GetAsync("/.well-known/webfinger?resource=acct:first@localhost");
        var secondResponse = await client.GetAsync("/.well-known/webfinger?resource=acct:first@localhost");

        Assert.True(firstResponse.IsSuccessStatusCode);
        Assert.True(secondResponse.IsSuccessStatusCode);
    }
}
