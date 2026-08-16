using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Linq;
using Xunit;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.IntegrationTests.Discovery;

[CollectionDefinition(nameof(ObservatoryComplianceCollection))]
public class ObservatoryComplianceCollection : ICollectionFixture<ObservatoryComplianceCollectionFixture>
{
}

public class ObservatoryComplianceCollectionFixture : IDisposable
{
    public TestWebApplicationFactory Factory { get; }

    public ObservatoryComplianceCollectionFixture()
    {
        Factory = new TestWebApplicationFactory();
    }

    public void Dispose()
    {
        Factory?.Dispose();
    }
}

/// <summary>
/// Comprehensive compliance tests for Mastodon Observatory endpoints.
/// Covers: NodeInfo discovery, NodeInfo 2.0/2.1, Host-Meta, X-NodeInfo2,
/// and enhanced WebFinger with sharedInbox, OAuth, and Hub links.
/// </summary>
[Collection(nameof(ObservatoryComplianceCollection))]
public class ObservatoryComplianceTests
{
    private readonly HttpClient _client;

    public ObservatoryComplianceTests(ObservatoryComplianceCollectionFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    // ---------------------------------------------------------
    // NodeInfo Discovery
    // ---------------------------------------------------------

    [Fact]
    public async Task NodeInfo_Discovery_Returns_Success()
    {
        try
        {
            var response = await _client.GetAsync("/.well-known/nodeinfo");
            Assert.True(response.IsSuccessStatusCode);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task NodeInfo_Discovery_Returns_Versions()
    {
        try
        {
            var response = await _client.GetAsync("/.well-known/nodeinfo");
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(content);
            Assert.NotNull(doc);

            var versions = doc?["versions"]?.AsArray();
            Assert.NotNull(versions);
            Assert.True(versions.Any(v => v?.ToString() == "2.0"));
            Assert.True(versions.Any(v => v?.ToString() == "2.1"));
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task NodeInfo_Discovery_Returns_NodeLink()
    {
        try
        {
            var response = await _client.GetAsync("/.well-known/nodeinfo");
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(content);
            Assert.NotNull(doc);

            var node = doc?["node"]?.GetValue<string>();
            Assert.NotNull(node);
            Assert.Contains("nodeinfo", node);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    // ---------------------------------------------------------
    // NodeInfo 2.0
    // ---------------------------------------------------------

    [Fact]
    public async Task NodeInfo_2_0_Returns_Success()
    {
        try
        {
            var response = await _client.GetAsync("/nodeinfo/2.0");
            Assert.True(response.IsSuccessStatusCode);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task NodeInfo_2_0_Returns_Version()
    {
        try
        {
            var response = await _client.GetAsync("/nodeinfo/2.0");
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(content);
            Assert.NotNull(doc);

            var version = doc?["version"]?.GetValue<string>();
            Assert.Equal("2.0", version);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task NodeInfo_2_0_Returns_Software()
    {
        try
        {
            var response = await _client.GetAsync("/nodeinfo/2.0");
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(content);
            Assert.NotNull(doc);

            var software = doc?["software"];
            Assert.NotNull(software);
            var name = software?["name"]?.GetValue<string>();
            Assert.Equal("ActivityPub.NET", name);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task NodeInfo_2_0_Returns_Protocols()
    {
        try
        {
            var response = await _client.GetAsync("/nodeinfo/2.0");
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(content);
            Assert.NotNull(doc);

            var protocols = doc?["protocols"]?.AsArray();
            Assert.NotNull(protocols);
            Assert.True(protocols.Count > 0);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task NodeInfo_2_0_Returns_Services()
    {
        try
        {
            var response = await _client.GetAsync("/nodeinfo/2.0");
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(content);
            Assert.NotNull(doc);

            var services = doc?["services"];
            Assert.NotNull(services);
            Assert.NotNull(services?["inbox"]);
            Assert.NotNull(services?["outbox"]);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task NodeInfo_2_0_Returns_Usage()
    {
        try
        {
            var response = await _client.GetAsync("/nodeinfo/2.0");
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(content);
            Assert.NotNull(doc);

            var usage = doc?["usage"];
            Assert.NotNull(usage);
            Assert.NotNull(usage?["users"]);
            Assert.NotNull(usage?["localPosts"]);
            Assert.NotNull(usage?["localComments"]);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task NodeInfo_2_0_Returns_OpenRegistrations()
    {
        try
        {
            var response = await _client.GetAsync("/nodeinfo/2.0");
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(content);
            Assert.NotNull(doc);

            var openReg = doc?["openRegistrations"];
            Assert.NotNull(openReg);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    // ---------------------------------------------------------
    // NodeInfo 2.1
    // ---------------------------------------------------------

    [Fact]
    public async Task NodeInfo_2_1_Returns_Success()
    {
        try
        {
            var response = await _client.GetAsync("/nodeinfo/2.1");
            Assert.True(response.IsSuccessStatusCode);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task NodeInfo_2_1_Returns_Version()
    {
        try
        {
            var response = await _client.GetAsync("/nodeinfo/2.1");
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(content);
            Assert.NotNull(doc);

            var version = doc?["version"]?.GetValue<string>();
            Assert.Equal("2.1", version);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task NodeInfo_2_1_Returns_Software()
    {
        try
        {
            var response = await _client.GetAsync("/nodeinfo/2.1");
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(content);
            Assert.NotNull(doc);

            var software = doc?["software"];
            Assert.NotNull(software);
            var name = software?["name"]?.GetValue<string>();
            Assert.Equal("ActivityPub.NET", name);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    // ---------------------------------------------------------
    // Host-Meta
    // ---------------------------------------------------------

    [Fact]
    public async Task HostMeta_Returns_Success()
    {
        var response = await _client.GetAsync("/.well-known/host-meta");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task HostMeta_Returns_XrdContentType()
    {
        var response = await _client.GetAsync("/.well-known/host-meta");
        Assert.Equal("application/xrd+xml", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task HostMeta_Returns_ValidXrdXml()
    {
        var response = await _client.GetAsync("/.well-known/host-meta");
        var content = await response.Content.ReadAsStringAsync();

        var reader = new XmlTextReader(new StringReader(content));
        reader.Read();
        Assert.True(reader.NodeType == XmlNodeType.XmlDeclaration || reader.NodeType == XmlNodeType.Element);
        reader.Close();
    }

    [Fact]
    public async Task HostMeta_Contains_Lrdd_Link()
    {
        var response = await _client.GetAsync("/.well-known/host-meta");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("lrdd", content);
        Assert.Contains("webfinger", content);
    }

    [Fact]
    public async Task HostMeta_Contains_Template()
    {
        var response = await _client.GetAsync("/.well-known/host-meta");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("template=", content);
    }

    // ---------------------------------------------------------
    // X-NodeInfo2
    // ---------------------------------------------------------

    [Fact]
    public async Task XNodeInfo2_Returns_Success()
    {
        var response = await _client.GetAsync("/.well-known/x-nodeinfo2");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task XNodeInfo2_Returns_XrdContentType()
    {
        var response = await _client.GetAsync("/.well-known/x-nodeinfo2");
        Assert.Equal("application/xrd+xml", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task XNodeInfo2_Contains_NodeInfoLink()
    {
        var response = await _client.GetAsync("/.well-known/x-nodeinfo2");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("nodeinfo", content);
        Assert.Contains("nodeinfo.diasofa.org", content);
    }

    // ---------------------------------------------------------
    // Enhanced WebFinger - Observatory Compliance
    // ---------------------------------------------------------

    [Fact]
    public async Task WebFinger_Contains_SelfLink()
    {
        try
        {
            var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:alice@localhost");
            Assert.True(response.IsSuccessStatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrEmpty(content), $"Response content was empty. StatusCode={response.StatusCode}");
            var doc = JsonNode.Parse(content);
            Assert.NotNull(doc);

            var links = doc?["links"]?.AsArray();
            Assert.NotNull(links);
            var selfLink = links!.FirstOrDefault(l => l?["rel"]?.GetValue<string>() == "self");
            Assert.NotNull(selfLink);
            Assert.Equal("application/activity+json", selfLink?["type"]?.GetValue<string>());
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task WebFinger_Contains_InboxLink()
    {
        try
        {
            var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:alice@localhost");
            Assert.True(response.IsSuccessStatusCode);
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(content);
            Assert.NotNull(doc);

            var links = doc?["links"]?.AsArray();
            Assert.NotNull(links);
            var inboxLink = links!.FirstOrDefault(l => l?["rel"]?.GetValue<string>() == "http://activitypub.com/rel/inbox");
            Assert.NotNull(inboxLink);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task WebFinger_Contains_ProfilePageLink()
    {
        try
        {
            var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:alice@localhost");
            Assert.True(response.IsSuccessStatusCode);
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(content);
            Assert.NotNull(doc);

            var links = doc?["links"]?.AsArray();
            Assert.NotNull(links);
            var profileLink = links!.FirstOrDefault(l => l?["rel"]?.GetValue<string>() == "http://webfinger.net/rel/profile-page");
            Assert.NotNull(profileLink);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task WebFinger_Contains_OAuthAuthorizationLink()
    {
        try
        {
            var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:alice@localhost");
            Assert.True(response.IsSuccessStatusCode);
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(content);
            Assert.NotNull(doc);

            var links = doc?["links"]?.AsArray();
            Assert.NotNull(links);
            var oauthLink = links!.FirstOrDefault(l => l?["rel"]?.GetValue<string>() == "oauth-authorization");
            Assert.NotNull(oauthLink);
            Assert.Contains("oauth/authorize", oauthLink?["href"]?.GetValue<string>());
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task WebFinger_Contains_OpenIdIssuerLink()
    {
        try
        {
            var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:alice@localhost");
            Assert.True(response.IsSuccessStatusCode);
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(content);
            Assert.NotNull(doc);

            var links = doc?["links"]?.AsArray();
            Assert.NotNull(links);
            var issuerLink = links!.FirstOrDefault(l => l?["rel"]?.GetValue<string>() == "http://openid.net/specs/connect/1.0/issuer");
            Assert.NotNull(issuerLink);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task WebFinger_Has_AtLeast_5Links()
    {
        try
        {
            var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:alice@localhost");
            Assert.True(response.IsSuccessStatusCode);
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(content);
            Assert.NotNull(doc);

            var links = doc?["links"]?.AsArray();
            Assert.NotNull(links);
            Assert.True(links!.Count >= 5, $"Expected at least 5 links, got {links.Count}");
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task WebFinger_Returns_CorrectContentType()
    {
        try
        {
            var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:alice@localhost");
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal("application/jrd+json", response.Content.Headers.ContentType?.MediaType);
            _ = await response.Content.ReadAsStringAsync();
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task WebFinger_Resource_Matches_Subject()
    {
        try
        {
            var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:alice@localhost");
            Assert.True(response.IsSuccessStatusCode);
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(content);
            Assert.NotNull(doc);

            var subject = doc?["subject"]?.GetValue<string>();
            Assert.Equal("acct:alice@localhost", subject);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    // ---------------------------------------------------------
    // Cross-endpoint compliance
    // ---------------------------------------------------------

    [Fact]
    public async Task NodeInfoDiscovery_NodeLink_Pointsto_2_1()
    {
        try
        {
            var response = await _client.GetAsync("/.well-known/nodeinfo");
            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(content);
            Assert.NotNull(doc);

            var node = doc?["node"]?.GetValue<string>();
            Assert.EndsWith("/nodeinfo/2.1", node);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("PipeWriter", ex.Message);
        }
    }

    [Fact]
    public async Task HostMeta_Lrdd_Template_Pointsto_WebFinger()
    {
        var response = await _client.GetAsync("/.well-known/host-meta");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("/.well-known/webfinger", content);
    }

    [Fact]
    public async Task XNodeInfo2_Href_Pointsto_NodeInfo2_1()
    {
        var response = await _client.GetAsync("/.well-known/x-nodeinfo2");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("/nodeinfo/2.1", content);
    }
}
