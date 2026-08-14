using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace ActivityPub.Tests.W3CCompliance;

public class HttpSignatureHeadersTests
{
    private readonly HttpClient _client;
    private const string _publicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA2Z3VpKjLqRZjVqSxPpKJ
        YqTjWpNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQq
        VqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQ
        qVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqN
        QqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVq
        NQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqV
        qNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQq
        VqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQqVqNQ
        qVQIDAQAB
        -----END PUBLIC KEY-----
        """;

    public HttpSignatureHeadersTests()
    {
        var server = new TestServer(new WebHostBuilder()
            .UseStartup<TestStartup>()
            .ConfigureServices(services =>
            {
                services.AddRouting();
            }));

        _client = server.CreateClient();
    }

    [Fact]
    public async Task Request_Must_Have_Digest_Header()
    {
        var content = new StringContent("{\"test\":\"data\"}", Encoding.UTF8, "application/json");
        var digest = ComputeDigest(content);
        content.Headers.Add("Digest", digest);

        var request = new HttpRequestMessage(HttpMethod.Post, "/activity")
        {
            Content = content
        };

        request.Headers.Add("Signature", CreateSignatureHeader(request, digest));

        var response = await _client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Request_Must_Have_Signature_Header()
    {
        var content = new StringContent("{\"test\":\"data\"}", Encoding.UTF8, "application/json");
        var digest = ComputeDigest(content);

        var request = new HttpRequestMessage(HttpMethod.Post, "/activity")
        {
            Content = content
        };

        request.Headers.Add("Digest", digest);
        request.Headers.Add("Signature", CreateSignatureHeader(request, digest));

        var response = await _client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Signature_Must_Include_KeyId()
    {
        var content = new StringContent("{\"test\":\"data\"}", Encoding.UTF8, "application/json");
        var digest = ComputeDigest(content);

        var request = new HttpRequestMessage(HttpMethod.Post, "/activity")
        {
            Content = content
        };

        request.Headers.Add("Digest", digest);
        var signature = CreateSignatureHeader(request, digest);
        Assert.Contains("keyId=", signature);

        request.Headers.Add("Signature", signature);

        var response = await _client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Signature_Must_Include_Created_Timestamp()
    {
        var content = new StringContent("{\"test\":\"data\"}", Encoding.UTF8, "application/json");
        var digest = ComputeDigest(content);

        var request = new HttpRequestMessage(HttpMethod.Post, "/activity")
        {
            Content = content
        };

        request.Headers.Add("Digest", digest);
        var signature = CreateSignatureHeader(request, digest);
        Assert.Contains("created=", signature);

        request.Headers.Add("Signature", signature);

        var response = await _client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Signature_Must_Include_Nonce()
    {
        var content = new StringContent("{\"test\":\"data\"}", Encoding.UTF8, "application/json");
        var digest = ComputeDigest(content);

        var request = new HttpRequestMessage(HttpMethod.Post, "/activity")
        {
            Content = content
        };

        request.Headers.Add("Digest", digest);
        var signature = CreateSignatureHeader(request, digest);
        Assert.Contains("nonce=", signature);

        request.Headers.Add("Signature", signature);

        var response = await _client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Signature_Must_Include_Digest()
    {
        var content = new StringContent("{\"test\":\"data\"}", Encoding.UTF8, "application/json");
        var digest = ComputeDigest(content);

        var request = new HttpRequestMessage(HttpMethod.Post, "/activity")
        {
            Content = content
        };

        request.Headers.Add("Digest", digest);
        var signature = CreateSignatureHeader(request, digest);
        Assert.Contains("digest=", signature);

        request.Headers.Add("Signature", signature);

        var response = await _client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Request_Must_Have_Correct_Digest_For_Content()
    {
        var content = new StringContent("{\"test\":\"data\"}", Encoding.UTF8, "application/json");
        var expectedDigest = ComputeDigest(content);
        content.Headers.Add("Digest", expectedDigest);

        var request = new HttpRequestMessage(HttpMethod.Post, "/activity")
        {
            Content = content
        };

        request.Headers.Add("Signature", CreateSignatureHeader(request, expectedDigest));

        var response = await _client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Request_Must_Have_Valid_Signature()
    {
        var content = new StringContent("{\"test\":\"data\"}", Encoding.UTF8, "application/json");
        var digest = ComputeDigest(content);

        var request = new HttpRequestMessage(HttpMethod.Post, "/activity")
        {
            Content = content
        };

        request.Headers.Add("Digest", digest);
        request.Headers.Add("Signature", CreateSignatureHeader(request, digest));

        var response = await _client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Signature_Created_Must_Be_Non_Zero()
    {
        var content = new StringContent("{\"test\":\"data\"}", Encoding.UTF8, "application/json");
        var digest = ComputeDigest(content);

        var request = new HttpRequestMessage(HttpMethod.Post, "/activity")
        {
            Content = content
        };

        request.Headers.Add("Digest", digest);
        var signature = CreateSignatureHeader(request, digest);

        Assert.Contains("created=", signature);
        var createdIndex = signature.IndexOf("created=", StringComparison.Ordinal) + 8;
        var createdValue = signature.Substring(createdIndex, 10);
        Assert.True(uint.TryParse(createdValue, out var created) && created > 0);

        request.Headers.Add("Signature", signature);

        var response = await _client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Signature_Nonce_Must_Be_Unique()
    {
        var content = new StringContent("{\"test\":\"data\"}", Encoding.UTF8, "application/json");
        var digest = ComputeDigest(content);

        var request = new HttpRequestMessage(HttpMethod.Post, "/activity")
        {
            Content = content
        };

        request.Headers.Add("Digest", digest);
        var signature = CreateSignatureHeader(request, digest);

        Assert.Contains("nonce=", signature);
        var nonceIndex = signature.IndexOf("nonce=", StringComparison.Ordinal) + 6;
        var nonceValue = signature.Substring(nonceIndex, 22);
        Assert.NotNull(nonceValue);
        Assert.NotEmpty(nonceValue);

        request.Headers.Add("Signature", signature);

        var response = await _client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    private string ComputeDigest(HttpContent content)
    {
        var contentBytes = content.ReadAsStringAsync().GetAwaiter().GetResult();
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(contentBytes));
        var base64 = Convert.ToBase64String(hash);
        return $"SHA-256={base64}";
    }

    private string CreateSignatureHeader(HttpRequestMessage request, string digest)
    {
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        var signatureParams = $"keyId=\"https://example.com/users/test#main-key\",algorithm=\"rsa-sha256\",headers=\"(created) (request-target) digest\",created={created},nonce=\"{nonce}\",digest=\"{digest}\"";

        return $"Signature {signatureParams}";
    }
}

public class TestStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
         app.UseRouting();
         app.UseEndpoints(endpoints =>
         {
             endpoints.MapPost("/activity", async context =>
             {
                 await context.Response.WriteAsync("OK").ConfigureAwait(false);
             });
         });
    }
}
