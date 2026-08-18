using ActivityPub.Core.Services;
using ActivityPub.WebUI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActivityPub.WebUI.Controllers;

/// <summary>
/// Server-side link previews for outbound URLs. The browser can't reach external
/// hosts or display remote images (CSP: connect-src / img-src 'self'), so the
/// preview is fetched here and the thumbnail is proxied through /image.
/// </summary>
[Authorize]
[Route("linkpreview")]
public class LinkPreviewController : Controller
{
    private readonly ILinkPreviewService _previewService;
    private readonly IHttpClientFactory _httpClientFactory;

    public LinkPreviewController(
        ILinkPreviewService previewService,
        IHttpClientFactory httpClientFactory)
    {
        _previewService = previewService;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Returns a JSON link-preview card for ?url=, or 404 when the URL is unsafe
    /// or no preview metadata could be extracted (the client then shows a bare
    /// link).
    /// </summary>
    [HttpGet]
    [Route("card")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Card([FromQuery] string url, CancellationToken cancellationToken)
    {
        var preview = await _previewService.GetPreviewAsync(url, cancellationToken);
        if (preview is null)
            return NotFound();
        return Json(preview);
    }

    /// <summary>
    /// Proxies a preview thumbnail through the server so the browser may display
    /// it under img-src 'self'. The URL is validated for the same SSRF rules as
    /// the card endpoint.
    /// </summary>
    [HttpGet]
    [Route("image")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Image([FromQuery] string url, CancellationToken cancellationToken)
    {
        var safe = LinkPreviewService.NormalizeUrl(url);
        if (safe is null)
            return NotFound();

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FediblogLinkPreview/1.0");

            using var response = await client.GetAsync(safe, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return NotFound();

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return NotFound();

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length > 2_000_000)
                return NotFound();

            return File(bytes, contentType);
        }
        catch
        {
            return NotFound();
        }
    }
}
