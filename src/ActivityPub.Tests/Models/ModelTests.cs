using ActivityPub.Core.Models;
using Xunit;

namespace ActivityPub.Tests.Models;

public class ModelTests
{
    [Fact]
    public void Object_Can_Be_Created_With_Minimal_Properties()
    {
        var obj = new ActivityPub.Core.Models.Object
        {
            Id = "https://example.com/notes/123",
            Type = "Note"
        };

        Assert.Equal("https://example.com/notes/123", obj.Id);
        Assert.Equal("Note", obj.Type);
    }

    [Fact]
    public void Actor_Can_Be_Created_With_Minimal_Properties()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/alice",
            Type = "Person"
        };

        Assert.Equal("https://example.com/users/alice", actor.Id);
        Assert.Equal("Person", actor.Type);
    }

    [Fact]
    public void PublicKey_Can_Be_Created_With_Minimal_Properties()
    {
        var key = new PublicKey
        {
            Id = "https://example.com/users/alice#main",
            Owner = "https://example.com/users/alice",
            PublicKeyPem = "-----BEGIN PUBLIC KEY-----"
        };

        Assert.Equal("https://example.com/users/alice#main", key.Id);
        Assert.Equal("https://example.com/users/alice", key.Owner);
        Assert.Equal("-----BEGIN PUBLIC KEY-----", key.PublicKeyPem);
    }

    [Fact]
    public void WebFingerLink_Can_Be_Created_With_Minimal_Properties()
    {
        var link = new WebFingerLink
        {
            Href = "https://example.com/users/alice"
        };

        Assert.Equal("https://example.com/users/alice", link.Href);
    }

    [Fact]
    public void WebFingerJrd_Can_Be_Created_With_Minimal_Properties()
    {
        var jrd = new WebFingerJrd
        {
            Subject = "acct:alice@example.com"
        };

        Assert.Equal("acct:alice@example.com", jrd.Subject);
    }

    [Fact]
    public void Activity_Can_Be_Created_With_Minimal_Properties()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Create"
        };

        Assert.Equal("https://example.com/activities/123", activity.Id);
        Assert.Equal("Create", activity.Type);
    }
}
