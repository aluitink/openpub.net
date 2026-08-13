using ActivityPub.Core.Models;
using Xunit;

namespace ActivityPub.Tests.Models;

public class ObjectTests
{
    [Fact]
    public void Object_Creates_With_All_Properties()
    {
        var obj = new ActivityPub.Core.Models.Object
        {
            Id = "https://example.com/notes/123",
            Type = "Note",
            Name = "Test Note",
            Content = "Test content",
            MediaType = "text/markdown",
            Url = "https://example.com/notes/123",
            AttributedTo = "https://example.com/users/alice",
            Published = DateTime.UtcNow,
            Updated = DateTime.UtcNow,
            InReplyTo = "https://example.com/notes/456",
            Parent = "https://example.com/threads/789",
            Tag = new[] { "tag1", "tag2" },
            Attachment = new[] { "https://example.com/attachments/1" },
            To = new[] { "https://example.com/users/bob" },
            Cc = new[] { "https://example.com/users/charlie" },
            Bcc = new[] { "https://example.com/users/dave" },
            Audience = "https://example.com/audience"
        };

        Assert.Equal("https://example.com/notes/123", obj.Id);
        Assert.Equal("Note", obj.Type);
        Assert.Equal("Test Note", obj.Name);
        Assert.Equal("Test content", obj.Content);
        Assert.Equal("text/markdown", obj.MediaType);
        Assert.Equal("https://example.com/users/alice", obj.AttributedTo);
        Assert.Equal("https://example.com/notes/456", obj.InReplyTo);
        Assert.Contains("tag1", obj.Tag!);
        Assert.Contains("https://example.com/attachments/1", obj.Attachment!);
        Assert.Contains("https://example.com/users/bob", obj.To!);
        Assert.Contains("https://example.com/users/charlie", obj.Cc!);
        Assert.Contains("https://example.com/users/dave", obj.Bcc!);
        Assert.Equal("https://example.com/audience", obj.Audience);
    }

    [Fact]
    public void Object_Has_Nullable_Properties()
    {
        var obj = new ActivityPub.Core.Models.Object
        {
            Id = "https://example.com/notes/123",
            Type = "Note"
        };

        Assert.Null(obj.Name);
        Assert.Null(obj.Content);
        Assert.Null(obj.Url);
        Assert.Null(obj.AttributedTo);
        Assert.Null(obj.Published);
        Assert.Null(obj.Updated);
        Assert.Null(obj.InReplyTo);
        Assert.Null(obj.Parent);
        Assert.Null(obj.Replies);
        Assert.Null(obj.Tag);
        Assert.Null(obj.Attachment);
        Assert.Null(obj.To);
        Assert.Null(obj.Cc);
        Assert.Null(obj.Bcc);
        Assert.Null(obj.Audience);
    }

    [Fact]
    public void Object_Serialization_RoundTrip()
    {
        var obj = new ActivityPub.Core.Models.Object
        {
            Id = "https://example.com/notes/123",
            Type = "Note",
            Name = "Test Note",
            Content = "Test content"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(obj);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<ActivityPub.Core.Models.Object>(json);

        Assert.Equal(obj.Id, deserialized?.Id);
        Assert.Equal(obj.Type, deserialized?.Type);
        Assert.Equal(obj.Name, deserialized?.Name);
        Assert.Equal(obj.Content, deserialized?.Content);
    }
}

public class ActorTests
{
    [Fact]
    public void Actor_Creates_With_All_Properties()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/alice",
            Type = "Person",
            Name = "Alice",
            PreferredUsername = "alice",
            Url = "https://example.com/users/alice",
            Inbox = "https://example.com/users/alice/inbox",
            Outbox = "https://example.com/users/alice/outbox",
            Followers = "https://example.com/users/alice/followers",
            Following = "https://example.com/users/alice/following",
            Liked = "https://example.com/users/alice/liked",
            Summary = "A test user",
            Published = DateTime.UtcNow,
            Updated = DateTime.UtcNow,
            ManuallyApprovesFollowers = false,
            Hashtag = true
        };

        Assert.Equal("https://example.com/users/alice", actor.Id);
        Assert.Equal("Person", actor.Type);
        Assert.Equal("Alice", actor.Name);
        Assert.Equal("alice", actor.PreferredUsername);
        Assert.Equal("https://example.com/users/alice/inbox", actor.Inbox);
        Assert.Equal("https://example.com/users/alice/outbox", actor.Outbox);
        Assert.Equal("https://example.com/users/alice/followers", actor.Followers);
        Assert.Equal("https://example.com/users/alice/following", actor.Following);
        Assert.Equal("https://example.com/users/alice/liked", actor.Liked);
        Assert.Equal("A test user", actor.Summary);
        Assert.False(actor.ManuallyApprovesFollowers);
        Assert.True(actor.Hashtag);
    }

    [Fact]
    public void Actor_Has_Nullable_Properties()
    {
        var actor = new Actor { Id = "https://example.com/users/alice" };

        Assert.Null(actor.Type);
        Assert.Null(actor.Name);
        Assert.Null(actor.PreferredUsername);
        Assert.Null(actor.Url);
        Assert.Null(actor.PublicKey);
        Assert.Null(actor.Inbox);
        Assert.Null(actor.Outbox);
        Assert.Null(actor.Followers);
        Assert.Null(actor.Following);
        Assert.Null(actor.Liked);
        Assert.Null(actor.Icon);
        Assert.Null(actor.Image);
        Assert.Null(actor.Summary);
        Assert.Null(actor.Published);
        Assert.Null(actor.Updated);
        Assert.Null(actor.Domain);
        Assert.Null(actor.Endpoints);
        Assert.Null(actor.SharedInbox);
        Assert.Null(actor.AdditionalProperties);
    }

    [Fact]
    public void Actor_Serialization_RoundTrip()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/alice",
            Type = "Person",
            Name = "Alice",
            PreferredUsername = "alice"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(actor);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<Actor>(json);

        Assert.Equal(actor.Id, deserialized?.Id);
        Assert.Equal(actor.Type, deserialized?.Type);
        Assert.Equal(actor.Name, deserialized?.Name);
        Assert.Equal(actor.PreferredUsername, deserialized?.PreferredUsername);
    }
}

public class PublicKeyTests
{
    [Fact]
    public void PublicKey_Creates_With_All_Properties()
    {
        var key = new PublicKey
        {
            Id = "https://example.com/users/alice#main",
            Owner = "https://example.com/users/alice",
            PublicKeyPem = "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG...\n-----END PUBLIC KEY-----"
        };

        Assert.Equal("https://example.com/users/alice#main", key.Id);
        Assert.Equal("https://example.com/users/alice", key.Owner);
        Assert.Equal("-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG...\n-----END PUBLIC KEY-----", key.PublicKeyPem);
    }

    [Fact]
    public void PublicKey_Serialization_RoundTrip()
    {
        var key = new PublicKey
        {
            Id = "https://example.com/users/alice#main",
            Owner = "https://example.com/users/alice",
            PublicKeyPem = "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG...\n-----END PUBLIC KEY-----"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(key);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<PublicKey>(json);

        Assert.Equal(key.Id, deserialized?.Id);
        Assert.Equal(key.Owner, deserialized?.Owner);
        Assert.Equal(key.PublicKeyPem, deserialized?.PublicKeyPem);
    }
}

public class WebFingerResponseTests
{
    [Fact]
    public void WebFingerResponse_Creates_With_All_Properties()
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

        Assert.Equal("acct:alice@example.com", response.Subject);
        Assert.Single(response.Links!);
        Assert.Equal("self", response.Links!.First().Rel);
        Assert.Equal("application/activity+json", response.Links!.First().Type);
        Assert.Equal("https://example.com/users/alice", response.Links!.First().Href);
    }

    [Fact]
    public void WebFingerResponse_Has_Nullable_Properties()
    {
        var response = new WebFingerResponse { Subject = "acct:alice@example.com" };

        Assert.NotNull(response.Links);
        Assert.Empty(response.Links);
    }

    [Fact]
    public void WebFingerResponse_Serialization_RoundTrip()
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

        var json = System.Text.Json.JsonSerializer.Serialize(response);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<WebFingerResponse>(json);

        Assert.Equal(response.Subject, deserialized?.Subject);
        Assert.NotNull(deserialized?.Links);
        Assert.Equal(response.Links!.Length, deserialized!.Links!.Length);
    }
}

public class WebFingerLinkTests
{
    [Fact]
    public void WebFingerLink_Creates_With_All_Properties()
    {
        var link = new WebFingerLink
        {
            Rel = "self",
            Type = "application/activity+json",
            Href = "https://example.com/users/alice"
        };

        Assert.Equal("self", link.Rel);
        Assert.Equal("application/activity+json", link.Type);
        Assert.Equal("https://example.com/users/alice", link.Href);
    }

    [Fact]
    public void WebFingerLink_Has_Nullable_Properties()
    {
        var link = new WebFingerLink { Href = "https://example.com/users/alice" };

        Assert.Null(link.Rel);
        Assert.Null(link.Type);
    }

    [Fact]
    public void WebFingerLink_Serialization_RoundTrip()
    {
        var link = new WebFingerLink
        {
            Rel = "self",
            Type = "application/activity+json",
            Href = "https://example.com/users/alice"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(link);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<WebFingerLink>(json);

        Assert.Equal(link.Rel, deserialized?.Rel);
        Assert.Equal(link.Type, deserialized?.Type);
        Assert.Equal(link.Href, deserialized?.Href);
    }
}

public class WebFingerJrdTests
{
    [Fact]
    public void WebFingerJrd_Creates_With_All_Properties()
    {
        var jrd = new WebFingerJrd
        {
            Subject = "acct:alice@example.com",
            Links = new List<WebFingerLink>
            {
                new WebFingerLink
                {
                    Rel = "self",
                    Type = "application/activity+json",
                    Href = "https://example.com/users/alice"
                }
            }
        };

        Assert.Equal("acct:alice@example.com", jrd.Subject);
        Assert.Single(jrd.Links!);
    }

    [Fact]
    public void WebFingerJrd_Serialization_RoundTrip()
    {
        var jrd = new WebFingerJrd
        {
            Subject = "acct:alice@example.com",
            Links = new List<WebFingerLink>
            {
                new WebFingerLink
                {
                    Rel = "self",
                    Type = "application/activity+json",
                    Href = "https://example.com/users/alice"
                }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(jrd);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<WebFingerJrd>(json);

        Assert.Equal(jrd.Subject, deserialized?.Subject);
        Assert.NotNull(deserialized?.Links);
        Assert.Equal(jrd.Links!.Count, deserialized!.Links!.Count);
    }
}

public class ActivityTests
{
    [Fact]
    public void Activity_Creates_With_All_Properties()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Create",
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/notes/456",
            Context = "https://example.com/contexts/789",
            To = new[] { "https://example.com/users/bob" },
            Cc = new[] { "https://example.com/users/charlie" },
            Published = DateTime.UtcNow
        };

        Assert.Equal("https://example.com/activities/123", activity.Id);
        Assert.Equal("Create", activity.Type);
        Assert.Equal("https://example.com/users/alice", activity.Actor);
        Assert.Equal("https://example.com/notes/456", activity.Object);
        Assert.Equal("https://example.com/contexts/789", activity.Context);
        Assert.Contains("https://example.com/users/bob", activity.To!);
        Assert.Contains("https://example.com/users/charlie", activity.Cc!);
    }

    [Fact]
    public void Activity_Has_Nullable_Properties()
    {
        var activity = new Activity { Id = "https://example.com/activities/123", Type = "Create" };

        Assert.Null(activity.Actor);
        Assert.Null(activity.Object);
        Assert.Null(activity.Context);
        Assert.Null(activity.To);
        Assert.Null(activity.Cc);
        Assert.Null(activity.Published);
    }

    [Fact]
    public void Activity_Serialization_RoundTrip()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Create",
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/notes/456"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(activity);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<Activity>(json);

        Assert.Equal(activity.Id, deserialized?.Id);
        Assert.Equal(activity.Type, deserialized?.Type);
    }
}
