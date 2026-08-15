using System.Text.Json;
using System.Text.Json.Serialization;
using ActivityPub.Core.Models;

namespace ActivityPub.Core.Infrastructure;

/// <summary>
/// Custom JSON converter for WebFinger JRD (JSON Resource Descriptor) responses
/// to improve serialization performance by avoiding reflection and reducing allocations.
/// </summary>
public sealed class WebFingerJsonConverter : JsonConverter<WebFingerJrd>
{
    public override WebFingerJrd? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // We only support writing, not reading
        throw new NotSupportedException("Reading JRD responses is not supported by this converter.");
    }

    public override void Write(Utf8JsonWriter writer, WebFingerJrd value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        
        // Write subject
        writer.WritePropertyName("subject");
        writer.WriteStringValue(value.Subject);
        
        // Write links array
        writer.WritePropertyName("links");
        writer.WriteStartArray();
        
        foreach (var link in value.Links)
        {
            writer.WriteStartObject();
            
            writer.WritePropertyName("rel");
            writer.WriteStringValue(link.Rel);
            
            writer.WritePropertyName("type");
            writer.WriteStringValue(link.Type);
            
            writer.WritePropertyName("href");
            writer.WriteStringValue(link.Href);
            
            writer.WriteEndObject();
        }
        
        writer.WriteEndArray();
        
        writer.WriteEndObject();
    }
}

// This class definition has been moved to ActivityPub.Core.Models.WebFingerJrd.cs
// This file only contains the JSON converter implementation

