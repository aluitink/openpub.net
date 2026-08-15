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
        throw new NotSupportedException("Reading JRD responses is not supported by this converter.");
    }

    public override void Write(Utf8JsonWriter writer, WebFingerJrd value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        
        writer.WritePropertyName("subject");
        writer.WriteStringValue(value.Subject);
        
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
