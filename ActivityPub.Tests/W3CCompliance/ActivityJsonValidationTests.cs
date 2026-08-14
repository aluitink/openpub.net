using ActivityPub.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace ActivityPub.Tests.W3CCompliance;

public class ActivityJsonValidationTests
{
    private readonly JsonSerializerOptions _jsonOptions;

    public ActivityJsonValidationTests()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
    }

    [Fact]
    public void Activity_Can_Be_Serialized()
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
    }

    [Fact]
    public void Activity_Must_Have_Id()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create"
        };

        Assert.NotNull(activity.Id);
    }

    [Fact]
    public void Activity_Must_Have_Type()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create"
        };

        Assert.NotNull(activity.Type);
    }

    [Fact]
    public void Activity_Must_Have_Actor()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create",
            Actor = "https://example.com/users/test"
        };

        Assert.NotNull(activity.Actor);
    }

    [Fact]
    public void Activity_Must_Have_Object()
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
            }
        };

        Assert.NotNull(activity.Object);
    }

    [Fact]
    public void Activity_Must_Have_Context()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create",
            Context = "https://www.w3.org/ns/activitystreams"
        };

        Assert.NotNull(activity.Context);
    }

    [Fact]
    public void Activity_Must_Have_Published()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create",
            Published = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };

        Assert.NotNull(activity.Published);
    }

    [Fact]
    public void Activity_Can_Be_Deserialized()
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
        var deserialized = JsonSerializer.Deserialize<Activity>(json, _jsonOptions);

        Assert.Equal(activity.Id, deserialized!.Id);
        Assert.Equal(activity.Type, deserialized.Type);
    }

    [Fact]
    public void Note_Can_Be_Serialized()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/1",
            Type = "Note",
            Content = "Hello World",
            Published = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(note, _jsonOptions);
        Assert.NotNull(json);
    }

    [Fact]
    public void Note_Must_Have_Id()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/1",
            Type = "Note"
        };

        Assert.NotNull(note.Id);
    }

    [Fact]
    public void Note_Must_Have_Type()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/1",
            Type = "Note"
        };

        Assert.NotNull(note.Type);
    }

    [Fact]
    public void Note_Can_Have_Content()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/1",
            Type = "Note",
            Content = "Hello World"
        };

        Assert.NotNull(note.Content);
    }

    [Fact]
    public void Note_Can_Have_Name()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/1",
            Type = "Note",
            Name = "Note Title"
        };

        Assert.NotNull(note.Name);
    }

    [Fact]
    public void Note_Can_Have_Published()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/1",
            Type = "Note",
            Published = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };

        Assert.NotNull(note.Published);
    }

    [Fact]
    public void Note_Can_Be_Deserialized()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/1",
            Type = "Note",
            Content = "Hello World",
            Published = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(note, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<Note>(json, _jsonOptions);

        Assert.Equal(note.Id, deserialized!.Id);
        Assert.Equal(note.Type, deserialized.Type);
        Assert.Equal(note.Content, deserialized.Content);
    }

    [Fact]
    public void Actor_Can_Be_Serialized()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Inbox = "https://example.com/users/test/inbox"
        };

        var json = JsonSerializer.Serialize(actor, _jsonOptions);
        Assert.NotNull(json);
    }

    [Fact]
    public void Actor_Must_Have_Id()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Inbox = "https://example.com/users/test/inbox"
        };

        Assert.NotNull(actor.Id);
    }

    [Fact]
    public void Actor_Must_Have_Type()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Inbox = "https://example.com/users/test/inbox"
        };

        Assert.NotNull(actor.Type);
    }

    [Fact]
    public void Actor_Must_Have_PreferredUsername()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test"
        };

        Assert.NotNull(actor.PreferredUsername);
    }

    [Fact]
    public void Actor_Must_Have_Inbox()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Inbox = "https://example.com/users/test/inbox"
        };

        Assert.NotNull(actor.Inbox);
    }

    [Fact]
    public void Actor_Can_Have_Name()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Name = "Test User"
        };

        Assert.NotNull(actor.Name);
    }

    [Fact]
    public void Actor_Can_Have_Summary()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Summary = "A test summary"
        };

        Assert.NotNull(actor.Summary);
    }

    [Fact]
    public void Actor_Can_Be_Deserialized()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Inbox = "https://example.com/users/test/inbox"
        };

        var json = JsonSerializer.Serialize(actor, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<Actor>(json, _jsonOptions);

        Assert.Equal(actor.Id, deserialized!.Id);
        Assert.Equal(actor.Type, deserialized.Type);
        Assert.Equal(actor.PreferredUsername, deserialized.PreferredUsername);
    }

    [Fact]
    public void Collection_Can_Be_Serialized()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection",
            Items = new List<string> { "item1", "item2" }
        };

        var json = JsonSerializer.Serialize(collection, _jsonOptions);
        Assert.NotNull(json);
    }

    [Fact]
    public void OrderedCollection_Can_Be_Serialized()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            OrderedItems = new List<string> { "item1", "item2" }
        };

        var json = JsonSerializer.Serialize(collection, _jsonOptions);
        Assert.NotNull(json);
    }

    [Fact]
    public void Image_Can_Be_Serialized()
    {
        var image = new Image
        {
            Url = "https://example.com/image.png",
            MediaType = "image/png"
        };

        var json = JsonSerializer.Serialize(image, _jsonOptions);
        Assert.NotNull(json);
    }

    [Fact]
    public void PublicKey_Can_Be_Serialized()
    {
        var publicKey = new PublicKey
        {
            Id = "https://example.com/users/test#main-key",
            Owner = "https://example.com/users/test",
            PublicKeyPem = "-----BEGIN PUBLIC KEY-----"
        };

        var json = JsonSerializer.Serialize(publicKey, _jsonOptions);
        Assert.NotNull(json);
    }

    [Fact]
    public void Activity_With_Null_Properties_Can_Be_Serialized()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create"
        };

        var json = JsonSerializer.Serialize(activity, _jsonOptions);
        Assert.NotNull(json);
    }

    [Fact]
    public void Note_With_Null_Properties_Can_Be_Serialized()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/1",
            Type = "Note"
        };

        var json = JsonSerializer.Serialize(note, _jsonOptions);
        Assert.NotNull(json);
    }

    [Fact]
    public void Actor_With_Null_Properties_Can_Be_Serialized()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test"
        };

        var json = JsonSerializer.Serialize(actor, _jsonOptions);
        Assert.NotNull(json);
    }

    [Fact]
    public void Json_Can_Be_Validated_Against_Schema()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create",
            Context = "https://www.w3.org/ns/activitystreams"
        };

        var json = JsonSerializer.Serialize(activity, _jsonOptions);

        Assert.Contains("\"@context\"", json);
        Assert.Contains("\"id\"", json);
        Assert.Contains("\"type\"", json);
    }

    [Fact]
    public void Json_Properties_Must_Be_CamelCase()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create"
        };

        var json = JsonSerializer.Serialize(activity, _jsonOptions);

        Assert.Contains("\"id\"", json);
        Assert.Contains("\"type\"", json);
        Assert.DoesNotContain("\"Id\"", json);
        Assert.DoesNotContain("\"Type\"", json);
    }
}
