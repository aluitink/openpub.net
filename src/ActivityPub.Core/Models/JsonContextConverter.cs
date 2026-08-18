using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Converts the JSON-LD <c>@context</c> field, which may be a single string,
/// an array of strings, or an object (with nested terms), to/from a
/// <see cref="JsonNode"/>. Real Mastodon actor documents ship
/// <c>"@context": ["https://www.w3.org/ns/activitystreams", ...]</c> — an
/// array — so a <c>string</c>-typed property would throw
/// <c>"cannot convert StartArray to string"</c> and silently discard the whole
/// document. This converter keeps parsing content-type- and shape-agnostic.
/// </summary>
public sealed class JsonContextConverter : JsonConverter<JsonNode?>
{
    public override JsonNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // JsonNode has no ParseValue; deserialize the current token (a string,
        // array, or object) into a JsonNode via the standard serializer.
        return JsonSerializer.Deserialize<JsonNode?>(ref reader, options);
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, JsonNode? value, JsonSerializerOptions options)
    {
        value?.WriteTo(writer);
    }
}
