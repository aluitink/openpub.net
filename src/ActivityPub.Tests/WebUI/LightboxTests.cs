using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class LightboxTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public LightboxTests(WebUIFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    [Fact]
    public async Task Layout_RendersLightboxOverlayMarkup()
    {
        var client = _factory.CreateClient();
        var html = await (await client.GetAsync("/")).Content.ReadAsStringAsync();

        Assert.Contains("lightbox-overlay", html);
        Assert.Contains("lightbox-dialog", html);
        Assert.Contains("lightbox-img", html);
        Assert.Contains("lightbox-close", html);
        // The dialog is an aria-modal dialog so screen readers treat it as a modal.
        Assert.Contains("role=\"dialog\"", html);
        Assert.Contains("aria-modal=\"true\"", html);
    }

    [Fact]
    public async Task LightboxJs_ImplementsKeyboardNavigation()
    {
        var client = _factory.CreateClient();
        var js = await (await client.GetAsync("/js/lightbox.js")).Content.ReadAsStringAsync();

        // Arrow keys + Home/End navigate the image set, Escape closes.
        Assert.Contains("ArrowLeft", js, StringComparison.Ordinal);
        Assert.Contains("ArrowRight", js, StringComparison.Ordinal);
        Assert.Contains("Home", js, StringComparison.Ordinal);
        Assert.Contains("End", js, StringComparison.Ordinal);
        Assert.Contains("Escape", js, StringComparison.Ordinal);
        // Focus is trapped inside the dialog and restored to the trigger on close.
        Assert.Contains("Tab", js, StringComparison.Ordinal);
        Assert.Contains("lastFocused", js, StringComparison.Ordinal);
        // Images are grouped per-note so a multi-image note is one lightbox set.
        Assert.Contains("data-lightbox-group", js, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LightboxCss_UsesTokensAndHighZIndex()
    {
        var client = _factory.CreateClient();
        var css = await (await client.GetAsync("/css/site.css")).Content.ReadAsStringAsync();

        var overlay = Regex.Match(css, @"\.lightbox-overlay\s*\{[^}]*\}", RegexOptions.Singleline);
        Assert.True(overlay.Success, "Expected a .lightbox-overlay rule in site.css.");
        Assert.Contains("z-index: 1300", overlay.Value, StringComparison.Ordinal);
        // The overlay is hidden by default via a [hidden] display rule.
        Assert.Contains(".lightbox-overlay[hidden] { display: none; }", css, StringComparison.Ordinal);

        // The full-screen backdrop is a near-opaque dark scrim.
        var img = Regex.Match(css, @"\.lightbox-img\s*\{[^}]*\}", RegexOptions.Singleline);
        Assert.True(img.Success, "Expected a .lightbox-img rule in site.css.");
        Assert.Contains("max-height: 88vh", img.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoteWithImage_RendersLightboxGroupAndAlt()
    {
        var client = await GetAuthenticatedClient();
        var unique = $"LightboxPost_{Guid.NewGuid().ToString("N")[..8]}";

        var form = new MultipartFormDataContent();
        form.Add(new StringContent(unique), "Content");
        form.Add(CreateFileContent("Image", "sunset.png", "image/png", "PNG bytes"), "Image");
        var post = await client.PostAsync("/compose/post", form);
        Assert.True(post.IsSuccessStatusCode || post.Headers.Location != null,
            $"Post with image failed: {(int)post.StatusCode}");

        var body = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();
        Assert.Contains(unique, body);

        // The uploaded image renders as a .note-image with a real alt (the
        // original filename) and a data-lightbox-index so the JS opens at the
        // right position. Its wrapper carries the per-note lightbox group.
        var imgTag = Regex.Matches(body, "<img[^>]*note-image[^>]*>")
            .Select(m => m.Value)
            .FirstOrDefault(t => t.Contains("sunset.png", StringComparison.Ordinal));
        Assert.False(string.IsNullOrEmpty(imgTag), "Expected the uploaded image to render as a .note-image <img>.");
        Assert.Contains("data-lightbox-index=\"0\"", imgTag, StringComparison.Ordinal);
        Assert.Contains("alt=\"sunset.png\"", imgTag, StringComparison.Ordinal);
        // The image is a keyboard-operable button so the lightbox is reachable
        // by keyboard alone (WCAG 2.1.1) and screen readers announce it.
        Assert.Contains("tabindex=\"0\"", imgTag, StringComparison.Ordinal);
        Assert.Contains("role=\"button\"", imgTag, StringComparison.Ordinal);
        Assert.Contains("aria-label=", imgTag, StringComparison.Ordinal);

        // The image wrapper is the lightbox group for this note.
        var wrapper = Regex.Match(body, "<div class=\"note-attachment note-images[^\"]*\" data-lightbox-group=\"[^\"]+\">");
        Assert.True(wrapper.Success, "Expected the image wrapper to carry data-lightbox-group.");
    }

    [Fact]
    public async Task NoteImage_WrapperCarriesGridClassAndLightboxGroup()
    {
        var client = await GetAuthenticatedClient();
        var unique = $"MultiPost_{Guid.NewGuid().ToString("N")[..8]}";
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(unique), "Content");
        form.Add(CreateFileContent("Image", "a.png", "image/png", "x"), "Image");
        var post = await client.PostAsync("/compose/post", form);
        Assert.True(post.IsSuccessStatusCode || post.Headers.Location != null);

        var body = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();
        Assert.Contains(unique, body);

        // The wrapper carries the note-images grid class and the lightbox group
        // (a full ActivityPub URL).
        var grid = Regex.Match(body, "<div class=\"note-attachment note-images note-images-1\" data-lightbox-group=\"https://[^\"]+\">");
        Assert.True(grid.Success, "Expected the image wrapper to carry the note-images grid class + lightbox group.");

        // The stylesheet defines the multi-image grid columns.
        var css = await (await client.GetAsync("/css/site.css")).Content.ReadAsStringAsync();
        Assert.Contains(".note-images {", css, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns", css, StringComparison.Ordinal);
    }

    async Task<HttpClient> GetAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        var username = $"lbx_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);
        await LoginUser(client, username);
        return client;
    }

    async Task RegisterUser(HttpClient client, string username)
    {
        var r = await client.PostAsync("/auth/register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "LB" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
        Assert.True(r.IsSuccessStatusCode || r.Headers.Location != null,
            $"Register failed: {(int)r.StatusCode}");
    }

    async Task LoginUser(HttpClient client, string username)
    {
        var r = await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" },
        }));
        Assert.True(r.IsSuccessStatusCode || r.Headers.Location != null,
            $"Login failed: {(int)r.StatusCode}");
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
}
