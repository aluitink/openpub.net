using ActivityPub.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace ActivityPub.Tests.W3CCompliance;

public class W3CComplianceTests
{
    private readonly JsonSerializerOptions _jsonOptions;

    public W3CComplianceTests()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
    }

    [Fact]
    public void Activity_Can_Be_Serialized_With_Minimal_Properties()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create"
        };

        var json = JsonSerializer.Serialize(activity, _jsonOptions);
        Assert.NotNull(json);
        Assert.Contains("\"id\":\"https://example.com/activities/1\"", json);
        Assert.Contains("\"type\":\"Create\"", json);
    }

    [Fact]
    public void Actor_Can_Be_Serialized_With_Minimal_Properties()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person"
        };

        var json = JsonSerializer.Serialize(actor, _jsonOptions);
        Assert.NotNull(json);
        Assert.Contains("\"id\":\"https://example.com/users/test\"", json);
        Assert.Contains("\"type\":\"Person\"", json);
    }

    [Fact]
    public void Object_Can_Be_Serialized_With_Minimal_Properties()
    {
        var obj = new Note
        {
            Id = "https://example.com/notes/1",
            Type = "Note"
        };

        var json = JsonSerializer.Serialize(obj, _jsonOptions);
        Assert.NotNull(json);
        Assert.Contains("\"id\":\"https://example.com/notes/1\"", json);
        Assert.Contains("\"type\":\"Note\"", json);
    }

    [Fact]
    public void Activity_With_Full_Properties_Serializes_Correctly()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create",
            Actor = "https://example.com/users/test",
            Object = new Note
            {
                Id = "https://example.com/notes/1",
                Type = "Note"
            },
            Context = "https://www.w3.org/ns/activitystreams",
            Published = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(activity, _jsonOptions);
        Assert.NotNull(json);
        
        var deserialized = JsonSerializer.Deserialize<Activity>(json, _jsonOptions);
        Assert.NotNull(deserialized);
        Assert.Equal(activity.Id, deserialized.Id);
        Assert.Equal(activity.Type, deserialized.Type);
    }

    [Theory]
    [InlineData("Create", "https://example.com/users/test", "https://example.com/notes/1")]
    [InlineData("Like", "https://example.com/users/alice", "https://example.com/notes/456")]
    [InlineData("Announce", "https://example.com/users/bob", "https://example.com/activities/789")]
    public void Activity_Type_Actor_And_Object_Can_Be_Serialized(
        string type, string actor, string objectId)
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = type,
            Actor = actor,
            Object = objectId
        };

        var json = JsonSerializer.Serialize(activity, _jsonOptions);
        Assert.Contains($"\"type\":\"{type}\"", json);
        Assert.Contains($"\"actor\":\"{actor}\"", json);
        Assert.Contains($"\"object\":\"{objectId}\"", json);
    }

    [Fact]
    public void WebFingerResponse_With_Standard_Link_Serializes_Correctly()
    {
        var response = new WebFingerResponse
        {
            Subject = "acct:alice@example.com",
            Links = new[]
            {
                new WebFingerLink
                {
                    Rel = "self",
                    Type = "application/activity+json",
                    Href = "https://example.com/users/alice"
                }
            }
        };

        var json = JsonSerializer.Serialize(response, _jsonOptions);
        Assert.NotNull(json);
        Assert.Contains("\"subject\":\"acct:alice@example.com\"", json);
        Assert.Contains("\"rel\":\"self\"", json);
        
        var deserialized = JsonSerializer.Deserialize<WebFingerResponse>(json, _jsonOptions);
        Assert.NotNull(deserialized);
        Assert.Equal(response.Subject, deserialized?.Subject);
        Assert.Equal(1, deserialized?.Links?.Length);
    }

    [Fact]
    public void PublicKey_With_Pem_Can_Be_Serialized()
    {
        var key = new PublicKey
        {
            Id = "https://example.com/users/alice#main",
            Owner = "https://example.com/users/alice",
            PublicKeyPem = "-----BEGIN PUBLIC KEY-----MIIBIjANBgkq..."
        };

        var json = JsonSerializer.Serialize(key, _jsonOptions);
        Assert.NotNull(json);
        Assert.Contains("\"publicKeyPem\":\"-----BEGIN PUBLIC KEY-----MIIBIjANBgkq...\"", json);
    }

    [Fact]
    public void Activity_With_Array_Properties_Serializes_Correctly()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create",
            To = new[] { "https://example.com/users/bob" },
            Cc = new[] { "https://example.com/users/charlie" }
        };

        var json = JsonSerializer.Serialize(activity, _jsonOptions);
        Assert.Contains("\"to\":[\"https://example.com/users/bob\"]", json);
        Assert.Contains("\"cc\":[\"https://example.com/users/charlie\"]", json);
    }

    [Fact]
    public void Actor_With_Picture_Urls_Can_Be_Serialized()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            Icon = new Image { Url = "https://example.com/icon.png" }
        };

        var json = JsonSerializer.Serialize(actor, _jsonOptions);
        Assert.NotNull(json);
        Assert.Contains("\"icon\":{\"url\":\"https://example.com/icon.png\"", json);
    }

    [Fact]
    public void ActivityPub_Objects_Have_Correct_W3C_Context()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create",
            Context = "https://www.w3.org/ns/activitystreams"
        };

        Assert.Equal("https://www.w3.org/ns/activitystreams", activity.Context);
    }

    [Fact]
    public void OrderedCollection_Has_OrderedItems()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            OrderedItems = new List<string> { "item1", "item2", "item3" }
        };

        Assert.NotNull(collection.OrderedItems);
    }

    [Fact]
    public void Collection_Has_Items()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection",
            Items = new List<string> { "item1", "item2", "item3" }
        };

        Assert.NotNull(collection.Items);
    }

    [Fact]
    public void OrderedCollection_First_Is_Optional()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            First = "https://example.com/collection/1?page=1"
        };

        Assert.Equal("https://example.com/collection/1?page=1", collection.First);
    }

    [Fact]
    public void OrderedCollection_Last_Is_Optional()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            Last = "https://example.com/collection/1?page=last"
        };

        Assert.Equal("https://example.com/collection/1?page=last", collection.Last);
    }
}
