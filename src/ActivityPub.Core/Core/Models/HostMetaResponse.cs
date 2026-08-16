using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// XRD host-meta document for .well-known/host-meta
/// </summary>
public class HostMetaResponse
{
    [JsonPropertyName("XRD")]
    public HostMetaXrd Xrd { get; set; } = new();
}

public class HostMetaXrd
{
    [JsonPropertyName("xmlns")]
    [JsonIgnore]
    public string XmlNamespace { get; set; } = "http://docs.oasis-open.org/ns/xri/xrd-1.0";

    [JsonPropertyName("Link")]
    public HostMetaLink[] Links { get; set; } = Array.Empty<HostMetaLink>();
}

public class HostMetaLink
{
    [JsonPropertyName("rel")]
    public string Rel { get; set; } = string.Empty;

    [JsonPropertyName("template")]
    public string Template { get; set; } = string.Empty;
}
