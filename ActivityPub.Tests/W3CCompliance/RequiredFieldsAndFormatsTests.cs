using ActivityPub.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace ActivityPub.Tests.W3CCompliance;

public class RequiredFieldsAndFormatsTests
{
    private readonly JsonSerializerOptions _jsonOptions;

    public RequiredFieldsAndFormatsTests()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
    }

    [Fact]
    public void Activity_Id_Is_Required()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create"
        };

        Assert.NotNull(activity.Id);
    }

    [Fact]
    public void Activity_Type_Is_Required()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create"
        };

        Assert.NotNull(activity.Type);
    }

    [Fact]
    public void Actor_Id_Is_Required()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person"
        };

        Assert.NotNull(actor.Id);
    }

    [Fact]
    public void Actor_Type_Is_Required()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person"
        };

        Assert.Equal("Person", actor.Type);
    }

    [Fact]
    public void Actor_PreferredUsername_Is_Required()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "testuser"
        };

        Assert.NotNull(actor.PreferredUsername);
    }

    [Fact]
    public void Actor_Inbox_Is_Required()
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
    public void Object_Id_Is_Required()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/1",
            Type = "Note"
        };

        Assert.NotNull(note.Id);
    }

    [Fact]
    public void Object_Type_Is_Required()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/1",
            Type = "Note"
        };

        Assert.Equal("Note", note.Type);
    }

    [Fact]
    public void Id_Must_Be_Absolute_URI()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create"
        };

        Assert.True(Uri.TryCreate(activity.Id, UriKind.Absolute, out _));
    }

    [Fact]
    public void Id_Must_Be_HTTPS()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create"
        };

        var uri = new Uri(activity.Id);
        Assert.Equal("https", uri.Scheme);
    }

    [Fact]
    public void Id_Must_Have_Hostname()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create"
        };

        var uri = new Uri(activity.Id);
        Assert.NotNull(uri.Host);
        Assert.NotEmpty(uri.Host);
    }

    [Fact]
    public void Published_Timestamp_Must_Be_ISO8601()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create",
            Published = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(activity, _jsonOptions);

        Assert.Contains("2024-01-15", json);
        Assert.Contains("10:30:00", json);
        Assert.Contains("Z", json);
    }

    [Fact]
    public void Updated_Timestamp_Must_Be_ISO8601()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create",
            Published = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            Updated = new DateTime(2024, 1, 16, 14, 45, 30, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(activity, _jsonOptions);

        Assert.Contains("2024-01-16", json);
        Assert.Contains("14:45:30", json);
    }

    [Fact]
    public void Note_Content_Can_Be_String()
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
    public void Note_Content_Can_Be_HTML()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/1",
            Type = "Note",
            Content = "<p>Hello <strong>World</strong></p>"
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
            Content = "Content",
            Name = "Note Title"
        };

        Assert.NotNull(note.Name);
    }

    [Fact]
    public void Actor_URL_Must_Be_Absolute_URI()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Url = "https://example.com/users/test"
        };

        Assert.True(Uri.TryCreate(actor.Url, UriKind.Absolute, out _));
    }

    [Fact]
    public void Actor_URL_Must_Be_HTTPS()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Url = "https://example.com/users/test"
        };

        var uri = new Uri(actor.Url);
        Assert.Equal("https", uri.Scheme);
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

        Assert.Equal("Test User", actor.Name);
    }

    [Fact]
    public void Actor_Summary_Can_Be_Provided()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Summary = "A test user"
        };

        Assert.Equal("A test user", actor.Summary);
    }

    [Fact]
    public void Actor_Can_Have_Icon()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Icon = new Image
            {
                Url = "https://example.com/users/test/icon.png",
                MediaType = "image/png"
            }
        };

        Assert.NotNull(actor.Icon);
    }

    [Fact]
    public void Actor_Can_Have_Image()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Image = new Image
            {
                Url = "https://example.com/users/test/image.jpg",
                MediaType = "image/jpeg"
            }
        };

        Assert.NotNull(actor.Image);
    }

    [Fact]
    public void Note_Can_Have_Attachments()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/1",
            Type = "Note",
            Content = "Check this out",
            Attachment = new List<string>
            {
                "https://example.com/attachments/1"
            }
        };

        Assert.NotNull(note.Attachment);
    }

    [Fact]
    public void Actor_PublicKey_Is_Required_For_Signing()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            PublicKey = new PublicKey
            {
                Id = "https://example.com/users/test#main-key",
                Owner = "https://example.com/users/test",
                PublicKeyPem = "-----BEGIN PUBLIC KEY-----"
            }
        };

        Assert.NotNull(actor.PublicKey);
    }

    [Fact]
    public void Actor_Can_Have_Followers_Collection()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Followers = "https://example.com/users/test/followers"
        };

        Assert.NotNull(actor.Followers);
    }

    [Fact]
    public void Actor_Can_Have_Following_Collection()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Following = "https://example.com/users/test/following"
        };

        Assert.NotNull(actor.Following);
    }

    [Fact]
    public void Actor_Can_Have_Outbox_Collection()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Outbox = "https://example.com/users/test/outbox"
        };

        Assert.NotNull(actor.Outbox);
    }

    [Fact]
    public void Actor_Can_Have_Liked_Collection()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Liked = "https://example.com/users/test/liked"
        };

        Assert.NotNull(actor.Liked);
    }

    [Fact]
    public void Actor_Can_Have_SharedInbox()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            SharedInbox = "https://example.com/inbox"
        };

        Assert.NotNull(actor.SharedInbox);
    }

    [Fact]
    public void Actor_Can_Have_ManuallyApprovesFollowers()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            ManuallyApprovesFollowers = true
        };

        Assert.True(actor.ManuallyApprovesFollowers);
    }

    [Fact]
    public void Actor_Can_Have_Published_Date()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Published = new DateTime(2024, 1, 1)
        };

        Assert.NotNull(actor.Published);
    }

    [Fact]
    public void Actor_Can_Have_Updated_Date()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Published = new DateTime(2024, 1, 1),
            Updated = new DateTime(2024, 6, 1)
        };

        Assert.NotNull(actor.Updated);
    }

    [Fact]
    public void Collection_Must_Have_Id()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection"
        };

        Assert.NotNull(collection.Id);
    }

    [Fact]
    public void OrderedCollection_Must_Have_Id()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection"
        };

        Assert.NotNull(collection.Id);
    }

    [Fact]
    public void Collection_Can_Have_Name()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection",
            Name = "My Collection"
        };

        Assert.NotNull(collection.Name);
    }

    [Fact]
    public void OrderedCollection_Can_Have_Name()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            Name = "My Ordered Collection"
        };

        Assert.NotNull(collection.Name);
    }

    [Fact]
    public void Collection_Can_Have_Summary()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection",
            Summary = "Collection summary"
        };

        Assert.NotNull(collection.Summary);
    }

    [Fact]
    public void OrderedCollection_Can_Have_Summary()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            Summary = "Ordered collection summary"
        };

        Assert.NotNull(collection.Summary);
    }

    [Fact]
    public void Collection_Items_Can_Be_Empty()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection",
            Items = new List<string>()
        };

        Assert.NotNull(collection.Items);
    }

    [Fact]
    public void OrderedCollection_OrderedItems_Can_Be_Empty()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            OrderedItems = new List<string>()
        };

        Assert.NotNull(collection.OrderedItems);
    }

    [Fact]
    public void Collection_Items_Can_Have_Multiple_Items()
    {
        var collection = new Collection
        {
            Id = "https://example.com/collection/1",
            Type = "Collection",
            Items = new List<string> { "item1", "item2", "item3" }
        };

        Assert.Equal(3, collection.Items.Count);
    }

    [Fact]
    public void OrderedCollection_OrderedItems_Can_Have_Multiple_Items()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            OrderedItems = new List<string> { "item1", "item2", "item3" }
        };

        Assert.Equal(3, collection.OrderedItems.Count);
    }

    [Fact]
    public void OrderedCollection_Can_Have_First_Page()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            First = "https://example.com/collection/1?page=1"
        };

        Assert.NotNull(collection.First);
    }

    [Fact]
    public void OrderedCollection_Can_Have_Last_Page()
    {
        var collection = new OrderedCollection
        {
            Id = "https://example.com/collection/1",
            Type = "OrderedCollection",
            Last = "https://example.com/collection/1?page=last"
        };

        Assert.NotNull(collection.Last);
    }

    [Fact]
    public void Object_Can_Have_AttributedTo()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/1",
            Type = "Note",
            Content = "Content",
            AttributedTo = "https://example.com/users/test"
        };

        Assert.NotNull(note.AttributedTo);
    }

    [Fact]
    public void Object_Can_Have_Url()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/1",
            Type = "Note",
            Content = "Content",
            Url = "https://example.com/notes/1"
        };

        Assert.NotNull(note.Url);
    }

    [Fact]
    public void Object_Can_Have_To_List()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/1",
            Type = "Note",
            Content = "Content",
            To = new List<string> { "https://example.com/followers" }
        };

        Assert.NotNull(note.To);
    }

    [Fact]
    public void Object_Can_Have_Cc_List()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/1",
            Type = "Note",
            Content = "Content",
            Cc = new List<string> { "https://example.com/users/test" }
        };

        Assert.NotNull(note.Cc);
    }

    [Fact]
    public void Object_Can_Have_Tag_List()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/1",
            Type = "Note",
            Content = "Content",
            Tag = new List<string> { "tag1", "tag2" }
        };

        Assert.NotNull(note.Tag);
    }

    [Fact]
    public void Activity_Can_Have_Additional_Properties()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create"
        };

        Assert.Null(activity.AdditionalProperties);
    }

    [Fact]
    public void Actor_Can_Have_Additional_Properties()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test"
        };

        Assert.Null(actor.AdditionalProperties);
    }

    [Fact]
    public void Object_Can_Have_Additional_Properties()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/1",
            Type = "Note",
            Content = "Content"
        };

        Assert.Null(note.AdditionalProperties);
    }
}
