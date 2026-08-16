using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class DocumentAttachmentTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public DocumentAttachmentTests(WebUIFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    HttpClient CreateClient() => _factory.CreateClient();

    async Task RegisterUser(HttpClient client, string username)
    {
        var registerResponse = await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "Test" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
        Assert.True(registerResponse.IsSuccessStatusCode || registerResponse.Headers.Location != null,
            $"Register failed: {(int)registerResponse.StatusCode}");
    }

    async Task LoginUser(HttpClient client, string username)
    {
        var loginResponse = await client.PostAsync("/auth/login", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" },
        }));
        Assert.True(loginResponse.IsSuccessStatusCode || loginResponse.Headers.Location != null,
            $"Login failed: {(int)loginResponse.StatusCode}");
    }

    async Task<HttpClient> GetAuthenticatedClient()
    {
        var client = CreateClient();
        var username = $"doc_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);
        await LoginUser(client, username);
        return client;
    }

    [Fact]
    public async Task ComposePage_ShowsDocumentUploadField()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/compose");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("name=\"Document\"", body);
    }

    [Fact]
    public async Task PostWithDocument_CreatesNoteWithDocumentAttachment()
    {
        var client = await GetAuthenticatedClient();
        var uniqueContent = $"DocPost_{Guid.NewGuid().ToString("N")[..8]}";

        var form = new MultipartFormDataContent();
        form.Add(new StringContent(uniqueContent), "Content");
        form.Add(CreateFileContent("Document", "report.pdf", "application/pdf", "PDF content here"), "Document");

        var postResponse = await client.PostAsync("/compose/post", form);
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null,
            $"Post with document failed: {(int)postResponse.StatusCode}");

        var timelineResponse = await client.GetAsync("/timeline");
        Assert.Equal(HttpStatusCode.OK, timelineResponse.StatusCode);
        var body = await timelineResponse.Content.ReadAsStringAsync();
        Assert.Contains(uniqueContent, body);
        Assert.Contains("report.pdf", body);
        Assert.Contains("doc-attachment", body);
    }

    [Fact]
    public async Task PostWithImageAndDocument_CreatesNoteWithBothAttachments()
    {
        var client = await GetAuthenticatedClient();
        var uniqueContent = $"BothPost_{Guid.NewGuid().ToString("N")[..8]}";

        var form = new MultipartFormDataContent();
        form.Add(new StringContent(uniqueContent), "Content");
        form.Add(CreateFileContent("Image", "photo.png", "image/png", "PNG bytes"), "Image");
        form.Add(CreateFileContent("Document", "doc.pdf", "application/pdf", "PDF bytes"), "Document");

        var postResponse = await client.PostAsync("/compose/post", form);
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null,
            $"Post with image+document failed: {(int)postResponse.StatusCode}");

        var timelineResponse = await client.GetAsync("/timeline");
        Assert.Equal(HttpStatusCode.OK, timelineResponse.StatusCode);
        var body = await timelineResponse.Content.ReadAsStringAsync();
        Assert.Contains(uniqueContent, body);
        Assert.Contains("doc.pdf", body);
    }

    [Fact]
    public async Task PostWithoutDocument_DoesNotRenderDocumentAttachment()
    {
        var client = await GetAuthenticatedClient();
        var uniqueContent = $"NoDocPost_{Guid.NewGuid().ToString("N")[..8]}";

        var postResponse = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", uniqueContent },
        }));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null);

        var timelineResponse = await client.GetAsync("/timeline");
        var body = await timelineResponse.Content.ReadAsStringAsync();

        var idx = body.IndexOf(uniqueContent, StringComparison.Ordinal);
        Assert.True(idx >= 0);
        var cardSegment = body.Substring(idx, Math.Min(2000, body.Length - idx));
        Assert.DoesNotContain("doc-attachment", cardSegment);
    }

    static StreamContent CreateFileContent(string fieldName, string fileName, string contentType, string fileContent)
    {
        var bytes = Encoding.UTF8.GetBytes(fileContent);
        var content = new StreamContent(new MemoryStream(bytes));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("form-data")
        {
            Name = fieldName,
            FileName = fileName
        };
        return content;
    }

    static FormUrlEncodedContent CreateFormContent(Dictionary<string, string> fields)
    {
        return new FormUrlEncodedContent(fields);
    }
}
