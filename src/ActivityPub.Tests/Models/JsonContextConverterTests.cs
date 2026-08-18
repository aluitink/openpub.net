using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ActivityPub.Core.Models;
using Xunit;

namespace ActivityPub.Tests.Models;

/// <summary>
/// Unit tests for <see cref="JsonContextConverter"/> — the converter that keeps
/// the JSON-LD <c>@context</c> field shape-agnostic (a single string, an array
/// of strings, or an object with nested terms). Real Mastodon actor documents
/// ship <c>"@context": ["…"]</c> as an array, so a <c>string</c>-typed property
/// would throw and silently discard the whole document. These tests verify the
/// converter reads and writes every shape without losing data, and that it is
/// correctly wired onto <c>Actor.Context</c>.
/// </summary>
public class JsonContextConverterTests
{
    private sealed class ContextCarrier
    {
        [JsonConverter(typeof(JsonContextConverter))]
        public JsonNode? Context { get; set; }
    }

    private static JsonNode? ReadContext(string contextValue)
    {
        // Wrap the bare @context value in an object property so the reader is
        // positioned correctly when the converter reads it.
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonContextConverter());
        var json = "{\"Context\":" + contextValue + "}";
        return JsonSerializer.Deserialize<ContextCarrier>(json, options)?.Context;
    }

    [Fact]
    public void Read_StringContext_ReturnsJsonStringNode()
    {
        var node = ReadContext("\"https://www.w3.org/ns/activitystreams\"");

        Assert.NotNull(node);
        Assert.Equal("https://www.w3.org/ns/activitystreams", node!.GetValue<string>());
    }

    [Fact]
    public void Read_ArrayContext_ReturnsJsonArrayNode()
    {
        // The real-world Mastodon shape: an array of context strings.
        var node = ReadContext(
            "[\"https://www.w3.org/ns/activitystreams\", \"https://w3id.org/security/v1\"]");

        Assert.NotNull(node);
        var arr = node!.AsArray();
        Assert.Equal(2, arr.Count);
        Assert.Equal("https://www.w3.org/ns/activitystreams", arr[0]!.GetValue<string>());
        Assert.Equal("https://w3id.org/security/v1", arr[1]!.GetValue<string>());
    }

    [Fact]
    public void Read_ObjectContext_ReturnsJsonObjectNode()
    {
        // An object context with a nested term definition.
        var node = ReadContext(
            "{\"as\": \"https://www.w3.org/ns/activitystreams\", \"term\": \"http://purl.org/syndication/\"}");

        Assert.NotNull(node);
        var obj = node!.AsObject();
        Assert.Equal("https://www.w3.org/ns/activitystreams", obj["as"]!.GetValue<string>());
        Assert.Equal("http://purl.org/syndication/", obj["term"]!.GetValue<string>());
    }

    [Fact]
    public void Read_NullContext_ReturnsNull()
    {
        var node = ReadContext("null");

        Assert.Null(node);
    }

    [Fact]
    public void Write_ArrayContext_RoundTrips()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonContextConverter());

        var carrier = new ContextCarrier
        {
            Context = JsonNode.Parse("[\"https://www.w3.org/ns/activitystreams\"]")
        };

        var json = JsonSerializer.Serialize(carrier, options);

        // The array shape is preserved on the way out.
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("Context").ValueKind);
        Assert.Equal(1, doc.RootElement.GetProperty("Context").GetArrayLength());
    }

    [Fact]
    public void Write_NullContext_EmitsNull()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonContextConverter());

        var carrier = new ContextCarrier { Context = null };
        var json = JsonSerializer.Serialize(carrier, options);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("Context").ValueKind);
    }

    [Fact]
    public void ActorContext_WithArrayContext_DeserializesWithoutDiscardingDocument()
    {
        // The regression this converter exists for: a real Mastodon actor with an
        // array @context must deserialize into a usable Actor (not throw and
        // discard the whole document).
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonContextConverter());
        options.PropertyNameCaseInsensitive = true;

        var actorJson = """
        {
          "@context": ["https://www.w3.org/ns/activitystreams", "https://w3id.org/security/v1"],
          "id": "https://mastodon.world/users/RayvenMX",
          "type": "Person",
          "preferredUsername": "RayvenMX"
        }
        """;

        var actor = JsonSerializer.Deserialize<Actor>(actorJson, options);

        Assert.NotNull(actor);
        Assert.Equal("https://mastodon.world/users/RayvenMX", actor.Id);
        Assert.Equal("RayvenMX", actor.PreferredUsername);
        Assert.NotNull(actor.Context);
        var arr = actor.Context!.AsArray();
        Assert.Equal(2, arr.Count);
        Assert.Equal("https://www.w3.org/ns/activitystreams", arr[0]!.GetValue<string>());
    }
}
