using ActivityPub.Core.Services;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ActivityPub.WebUI.Services;

public class LinkPreviewService : ILinkPreviewService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LinkPreviewService> _logger;

    private static readonly TimeSpan PositiveCache = TimeSpan.FromHours(6);
    private static readonly TimeSpan NegativeCache = TimeSpan.FromMinutes(10);
    private static readonly int MaxHtmlBytes = 1_000_000;
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(8);

    // <meta ...> with an attribute we care about (og:*, twitter:*, description,
    // or a name/property of "keywords" is ignored). Attributes may be single or
    // double quoted, in any order, and the tag may be self-closing.
    private static readonly Regex MetaRegex = new(
        @"<meta\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttrRegex = new(
        @"([a-zA-Z0-9:_-]+)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))",
        RegexOptions.Compiled);

    private static readonly Regex TitleTagRegex = new(
        @"<title\b[^>]*>(.*?)</title>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public LinkPreviewService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<LinkPreviewService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<LinkPreview?> GetPreviewAsync(string url, CancellationToken cancellationToken = default)
    {
        var safe = NormalizeUrl(url);
        if (safe is null)
            return null;

        var key = "linkpreview:" + safe;
        if (_cache.TryGetValue(key, out LinkPreview? cached))
            return cached;

        LinkPreview? preview = await FetchOgAsync(safe, cancellationToken)
            ?? await FetchOEmbedAsync(safe, cancellationToken);

        _cache.Set(key, preview, preview is null ? NegativeCache : PositiveCache);
        return preview;
    }

    /// <summary>
    /// Validates the URL for SSRF safety: absolute http(s), not a local/private
    /// address, no credentials, and not a well-known metadata endpoint. Returns
    /// the canonical absolute URL, or null to skip.
    /// </summary>
    public static string? NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        url = url.Trim();
        // Strip surrounding markup some clients leave behind.
        url = url.Trim('<', '>', '(', ')', '"', '\'');
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !uri.IsAbsoluteUri)
            return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;
        if (uri.UserInfo.Length > 0)
            return null;
        if (string.IsNullOrEmpty(uri.Host))
            return null;

        var host = uri.Host;
        // Never fetch link-local / loopback / private ranges (SSRF guard). This
        // is a best-effort check: we resolve nothing, we just reject the obvious
        // internal shapes. DNS rebinding is out of scope for a v1 client feature.
        if (IsBlockedHost(host, uri))
            return null;

        return uri.ToString();
    }

    private static bool IsBlockedHost(string host, Uri uri)
    {
        var h = host.ToLowerInvariant();
        if (h == "localhost" || h.EndsWith(".local", StringComparison.Ordinal) ||
            h.EndsWith(".internal", StringComparison.Ordinal) || h == "metadata.google.internal")
            return true;
        if (uri.IsLoopback)
            return true;

        // IPv4 literals in the 10/8, 172.16/12, 192.168/16, 169.254/16 (incl. the
        // cloud metadata IP 169.254.169.254), 127/8, 0.0.0.0.
        if (System.Net.IPAddress.TryParse(host, out var ip))
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();
                if (b[0] == 127 || b[0] == 0) return true;
                if (b[0] == 10) return true;
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
                if (b[0] == 192 && b[1] == 168) return true;
                if (b[0] == 169 && b[1] == 254) return true;
            }
            if (ip.IsIPv6LinkLocal) return true;
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 &&
                (ip.Equals(System.Net.IPAddress.IPv6Loopback) || ip.Equals(System.Net.IPAddress.Any)))
                return true;
        }
        return false;
    }

    private async Task<LinkPreview?> FetchOgAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = FetchTimeout;
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/html"));
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FediblogLinkPreview/1.0");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(FetchTimeout);

            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (!response.IsSuccessStatusCode)
                return null;

            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var html = await ReadBoundedAsync(reader, MaxHtmlBytes, cts.Token);

            var preview = ParseOpenGraph(html, url);
            return preview;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Link preview fetch failed for {Url}", url);
            return null;
        }
    }

    private async Task<LinkPreview?> FetchOEmbedAsync(string url, CancellationToken cancellationToken)
    {
        // OEmbed fallback: try the standard /oembed endpoint. Only a handful of
        // providers (YouTube, Twitter/X, Vimeo, SoundCloud, Spotify, ...) expose
        // it at this exact path, so most sites 404 and we simply return null.
        var endpoint = url + (url.Contains("?", StringComparison.Ordinal) ? "&" : "?") +
                       "format=json&maxwidth=800";
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = FetchTimeout;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(FetchTimeout);

            using var response = await client.GetAsync(endpoint, cts.Token);
            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var title = GetString(root, "title");
            var html = GetString(root, "html");
            var thumbnail = GetString(root, "thumbnail_url") ?? GetString(root, "thumbnail");
            var provider = GetString(root, "provider_name");

            // If the OEmbed body is an HTML snippet, pull a title/description out.
            if (string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(html))
            {
                var t = TitleTagRegex.Match(html);
                if (t.Success) title = Unescape(t.Groups[1].Value).Trim();
            }

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(thumbnail))
                return null;

            return new LinkPreview
            {
                Url = url,
                Title = title ?? url,
                Description = GetString(root, "description"),
                Image = thumbnail,
                SiteName = provider,
                Source = "oembed"
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "OEmbed fetch failed for {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Parses OpenGraph / Twitter / basic meta tags from an HTML document into a
    /// <see cref="LinkPreview"/>. Returns null when no usable metadata exists.
    /// </summary>
    internal static LinkPreview? ParseOpenGraph(string? html, string url)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        // Map each <meta> tag's name/property -> content.
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in MetaRegex.Matches(html))
        {
            string? name = null;
            string? content = null;
            foreach (Match a in AttrRegex.Matches(m.Value))
            {
                var an = a.Groups[1].Value.ToLowerInvariant();
                var av = a.Groups[2].Success ? a.Groups[2].Value
                    : a.Groups[3].Success ? a.Groups[3].Value
                    : a.Groups[4].Value;
                if (an == "name" || an == "property") name = av;
                else if (an == "content") content = av;
            }
            if (name != null && content != null && !tags.ContainsKey(name))
                tags[name] = content;
        }

        string Pick(params string[] keys)
        {
            foreach (var k in keys)
                if (tags.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
                    return Unescape(v).Trim();
            return string.Empty;
        }

        var title = Pick("og:title", "twitter:title", "title");
        var description = Pick("og:description", "twitter:description", "description", "og:site_name", "author");
        var image = Pick("og:image", "og:image:url", "og:image:secure_url", "twitter:image", "twitter:image:src");
        var siteName = Pick("og:site_name");
        var author = Pick("twitter:creator", "author");

        // Fall back to the <title> tag for the title and a meta description.
        if (string.IsNullOrWhiteSpace(title))
        {
            var t = TitleTagRegex.Match(html);
            if (t.Success) title = Unescape(t.Groups[1].Value).Trim();
        }
        if (string.IsNullOrWhiteSpace(description))
        {
            // crude fallback: first line of <meta name="description"> already covered;
            // otherwise use the visible <title>.
            if (!string.IsNullOrWhiteSpace(title))
                description = title;
        }

        if (string.IsNullOrWhiteSpace(title))
            return null;

        var preview = new LinkPreview
        {
            Url = url,
            Title = title,
            Description = string.IsNullOrWhiteSpace(description) ? null : description,
            Image = string.IsNullOrWhiteSpace(image) ? null : image,
            SiteName = string.IsNullOrWhiteSpace(siteName) ? null : siteName,
            AuthorName = string.IsNullOrWhiteSpace(author) ? null : author,
            Source = "basic"
        };
        if (!string.IsNullOrWhiteSpace(Pick("og:title", "og:description", "og:image")) ||
            !string.IsNullOrWhiteSpace(Pick("twitter:title", "twitter:image")))
            preview.Source = "og";
        return preview;
    }

    private static string? GetString(JsonElement el, string prop)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var p) &&
            p.ValueKind == JsonValueKind.String)
            return p.GetString();
        return null;
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int maxChars, CancellationToken ct)
    {
        var sb = new StringBuilder(maxChars);
        var buffer = new char[8192];
        while (true)
        {
            int read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read == 0) break;
            sb.Append(buffer, 0, read);
            if (sb.Length >= maxChars) break;
        }
        return sb.ToString();
    }

    private static string Unescape(string s)
        => System.Net.WebUtility.HtmlDecode(s ?? "");
}
