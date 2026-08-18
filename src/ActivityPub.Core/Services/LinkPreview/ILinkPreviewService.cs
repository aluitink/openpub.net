namespace ActivityPub.Core.Services;

public interface ILinkPreviewService
{
    /// <summary>
    /// Fetches and parses a link preview for <paramref name="url"/>. Returns
    /// null when the URL is not a safe absolute http(s) link, the fetch fails,
    /// or no usable preview metadata can be extracted (so the UI falls back to a
    /// bare link). Results are cached in-memory to avoid re-fetching the same
    /// URL on every timeline render.
    /// </summary>
    Task<LinkPreview?> GetPreviewAsync(string url, CancellationToken cancellationToken = default);
}
