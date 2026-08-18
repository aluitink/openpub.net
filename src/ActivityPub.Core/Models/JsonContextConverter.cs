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
        // The current token is the entire @context value (a string, an array, or
        // an object). Parse it directly into a JsonNode rather than delegating to
        // JsonSerializer.Deserialize<JsonNode?>: on .NET 10 the built-in JsonNode
        // converter is itself a JsonConverter<JsonNode?>, so re-deserializing from
        // inside this converter would re-dispatch to this same converter and
        // recurse infinitely (stack overflow), crashing actor deserialization.
        // Reading the value as a JsonElement and re-parsing its raw text is
        // shape-agnostic and recursion-free.
        if (reader.TokenType == JsonTokenType.String)
        {
            return JsonValue.Create(reader.GetString());
        }
        if (reader.TokenType == JsonTokenType.Null || reader.TokenType == JsonTokenType.None)
        {
            return null;
        }

        var element = JsonDocument.ParseValue(ref reader).RootElement;
        return JsonNode.Parse(element.GetRawText());
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, JsonNode? value, JsonSerializerOptions options)
    {
        value?.WriteTo(writer);
    }
}
