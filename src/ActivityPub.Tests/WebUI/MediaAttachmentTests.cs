using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace ActivityPub.Tests.WebUI;

/// <summary>
/// Phase 51.3 — Rich media in the UI. Verifies that non-image attachments
/// (Video / Audio / Document) carried on a note are rendered with native
/// &lt;video&gt;/&lt;audio&gt; players (with poster thumbnails where available) and a
/// download card for documents, and that the view model exposes them.
/// </summary>
public class MediaAttachmentTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public MediaAttachmentTests(WebUIFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    [Fact]
    public async Task NoteWithVideo_RendersNativeVideoPlayerWithPoster()
    {
        var (client, username) = await GetAuthenticatedUser();
        var marker = $"vid_{Guid.NewGuid().ToString("N")[..8]}";
        await SeedMediaNoteAsync(username, marker, new[]
        {
            new Dictionary<string, string>
            {
                { "type", "Video" },
                { "url", $"https://localhost/media/{marker}.mp4" },
                { "mediaType", "video/mp4" },
                { "name", "demo-video.mp4" },
                { "preview", $"https://localhost/media/{marker}-poster.jpg" },
            },
        });

        var body = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();
        Assert.Contains(marker, body);

        // The video renders as a native <video> with controls and a poster.
        var video = Regex.Match(body, "<video[^>]*>");
        Assert.True(video.Success, "Expected a <video> element for the video attachment.");
        Assert.Contains("controls", video.Value, StringComparison.Ordinal);
        Assert.Contains("poster=\"https://localhost/media/", video.Value, StringComparison.Ordinal);

        // The <source> carries the mp4 url + type.
        var source = Regex.Match(body, "<source[^>]*video/mp4[^>]*>");
        Assert.True(source.Success, "Expected a <source> with the mp4 url + media type.");
        Assert.Contains($"{marker}.mp4", source.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoteWithAudio_RendersNativeAudioPlayer()
    {
        var (client, username) = await GetAuthenticatedUser();
        var marker = $"aud_{Guid.NewGuid().ToString("N")[..8]}";
        await SeedMediaNoteAsync(username, marker, new[]
        {
            new Dictionary<string, string>
            {
                { "type", "Audio" },
                { "url", $"https://localhost/media/{marker}.mp3" },
                { "mediaType", "audio/mpeg" },
                { "name", "podcast-episode.mp3" },
            },
        });

        var body = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();
        Assert.Contains(marker, body);

        // The audio renders as a native <audio> with controls; no <video>.
        var audio = Regex.Match(body, "<audio[^>]*>");
        Assert.True(audio.Success, "Expected an <audio> element for the audio attachment.");
        Assert.Contains("controls", audio.Value, StringComparison.Ordinal);
        var source = Regex.Match(body, "<source[^>]*audio/mpeg[^>]*>");
        Assert.True(source.Success, "Expected a <source> with the mp3 url + media type.");
        Assert.Contains($"{marker}.mp3", source.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoteWithDocument_RendersDownloadCard()
    {
        var (client, username) = await GetAuthenticatedUser();
        var marker = $"doc_{Guid.NewGuid().ToString("N")[..8]}";
        await SeedMediaNoteAsync(username, marker, new[]
        {
            new Dictionary<string, string>
            {
                { "type", "Document" },
                { "url", $"https://localhost/media/{marker}.pdf" },
                { "mediaType", "application/pdf" },
                { "name", "report.pdf" },
            },
        });

        var body = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();
        Assert.Contains(marker, body);

        // The document renders as a download <a> whose href is the file url.
        var doc = Regex.Match(body, "<a[^>]*href=\"https://localhost/media/[^>]*download[^>]*>");
        Assert.True(doc.Success, "Expected a download <a> for the document attachment.");
        Assert.Contains($"{marker}.pdf", doc.Value, StringComparison.Ordinal);
        // The file name is shown as the link text.
        Assert.Contains("report.pdf", body, StringComparison.Ordinal);

        // A document is NOT a media player.
        Assert.DoesNotMatch(new Regex($@"<video[^>]*>\s*<source[^>]*{marker}"), body);
        Assert.DoesNotMatch(new Regex($@"<audio[^>]*>\s*<source[^>]*{marker}"), body);
    }

    [Fact]
    public async Task MixedMedia_RendersAllKindsInOneNote()
    {
        var (client, username) = await GetAuthenticatedUser();
        var marker = $"mix_{Guid.NewGuid().ToString("N")[..8]}";
        await SeedMediaNoteAsync(username, marker, new[]
        {
            new Dictionary<string, string>
            {
                { "type", "Video" },
                { "url", $"https://localhost/media/{marker}.mp4" },
                { "mediaType", "video/mp4" },
                { "name", "clip.mp4" },
            },
            new Dictionary<string, string>
            {
                { "type", "Audio" },
                { "url", $"https://localhost/media/{marker}.mp3" },
                { "mediaType", "audio/mpeg" },
                { "name", "track.mp3" },
            },
            new Dictionary<string, string>
            {
                { "type", "Document" },
                { "url", $"https://localhost/media/{marker}.pdf" },
                { "mediaType", "application/pdf" },
                { "name", "notes.pdf" },
            },
        });

        var body = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();
        Assert.Contains(marker, body);

        Assert.True(Regex.IsMatch(body, "<video[^>]*>"), "Mixed note should render a <video>.");
        Assert.True(Regex.IsMatch(body, "<audio[^>]*>"), "Mixed note should render an <audio>.");
        Assert.True(Regex.IsMatch(body, "<a[^>]*href=\"https://localhost/media/[^>]*download[^>]*>"), "Mixed note should render a download card.");
        Assert.Contains("notes.pdf", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VideoWithoutPoster_RendersVideoWithoutPosterAttr()
    {
        var (client, username) = await GetAuthenticatedUser();
        var marker = $"novid_{Guid.NewGuid().ToString("N")[..8]}";
        await SeedMediaNoteAsync(username, marker, new[]
        {
            new Dictionary<string, string>
            {
                { "type", "Video" },
                { "url", $"https://localhost/media/{marker}.webm" },
                { "mediaType", "video/webm" },
                { "name", "webm-clip.webm" },
            },
        });

        var body = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();
        Assert.Contains(marker, body);

        var video = Regex.Match(body, "<video[^>]*>");
        Assert.True(video.Success, "Expected a <video> element.");
        // No poster attribute is emitted when the attachment has no preview.
        Assert.DoesNotContain("poster=", video.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MediaCss_DefinesPlayersAndThumbnailStyles()
    {
        var client = _factory.CreateClient();
        var css = await (await client.GetAsync("/css/site.css")).Content.ReadAsStringAsync();

        Assert.Contains(".note-media {", css, StringComparison.Ordinal);
        Assert.Contains(".note-video {", css, StringComparison.Ordinal);
        Assert.Contains(".note-audio {", css, StringComparison.Ordinal);
        Assert.Contains(".note-audio-player {", css, StringComparison.Ordinal);
        // Video is capped so it never overflows the card.
        var video = Regex.Match(css, @"\.note-video\s*\{[^}]*\}", RegexOptions.Singleline);
        Assert.True(video.Success, "Expected a .note-video rule in site.css.");
        Assert.Contains("max-height", video.Value, StringComparison.Ordinal);
    }

    /// <summary>Seeds a single public note carrying the given attachments directly via the repository.</summary>
    async Task SeedMediaNoteAsync(string username, string marker, params Dictionary<string, string>[] attachments)
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
        var actor = await repo.GetUserActorAsync(username);
        Assert.NotNull(actor);

        var note = new Note
        {
            Id = $"https://localhost/users/{username}/notes/{Guid.NewGuid():N}",
            Type = "Note",
            Content = $"{marker}_media",
            AttributedTo = actor!.Id,
            Published = DateTime.UtcNow,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" },
            Attachment = attachments
                .Select(a => (object)JsonSerializer.SerializeToElement(a))
                .ToList()
        };
        var activity = new Activity
        {
            Id = $"https://localhost/users/{username}/activities/{Guid.NewGuid():N}",
            Type = "Create",
            Actor = actor.Id,
            Object = note,
            Published = DateTime.UtcNow,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" }
        };
        await repo.SaveActivityAsync(activity);
    }

    async Task<(HttpClient Client, string Username)> GetAuthenticatedUser()
    {
        var client = _factory.CreateClient();
        var username = $"media_{Guid.NewGuid().ToString("N")[..8]}";
        var register = await client.PostAsync("/auth/register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "Media" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
        Assert.True(register.IsSuccessStatusCode || register.Headers.Location != null,
            $"register failed: {(int)register.StatusCode}");
        var login = await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" },
        }));
        Assert.True(login.IsSuccessStatusCode || login.Headers.Location != null,
            $"login failed: {(int)login.StatusCode}");
        return (client, username);
    }
}
