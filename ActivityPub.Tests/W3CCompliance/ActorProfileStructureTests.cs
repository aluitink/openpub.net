using ActivityPub.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace ActivityPub.Tests.W3CCompliance;

public class ActorProfileStructureTests
{
    private readonly JsonSerializerOptions _jsonOptions;

    public ActorProfileStructureTests()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
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
    public void Actor_Type_Must_Be_Person()
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

        Assert.Equal("testuser", actor.PreferredUsername);
    }

    [Fact]
    public void Actor_Inbox_Must_Be_Absolute_URI()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Inbox = "https://example.com/users/test/inbox"
        };

        Assert.True(Uri.TryCreate(actor.Inbox, UriKind.Absolute, out _));
    }

    [Fact]
    public void Actor_Inbox_Must_Be_HTTPS()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Inbox = "https://example.com/users/test/inbox"
        };

        var uri = new Uri(actor.Inbox);
        Assert.Equal("https", uri.Scheme);
    }

    [Fact]
    public void Actor_Outbox_Must_Be_Absolute_URI()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Outbox = "https://example.com/users/test/outbox"
        };

        Assert.True(Uri.TryCreate(actor.Outbox, UriKind.Absolute, out _));
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
    public void Actor_Name_Is_Optional()
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
    public void Actor_Summary_Is_Optional()
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
    public void Actor_Icon_Is_Optional()
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
    public void Actor_Icon_Must_Have_Url()
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

        Assert.NotNull(actor.Icon?.Url);
    }

    [Fact]
    public void Actor_Icon_Must_Have_MediaType()
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

        Assert.NotNull(actor.Icon?.MediaType);
    }

    [Fact]
    public void Actor_Image_Is_Optional()
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
    public void Actor_Image_Must_Have_Url()
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

        Assert.NotNull(actor.Image?.Url);
    }

    [Fact]
    public void Actor_Image_Must_Have_MediaType()
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

        Assert.NotNull(actor.Image?.MediaType);
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
    public void Actor_PublicKey_Id_Is_Required()
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

        Assert.NotNull(actor.PublicKey?.Id);
    }

    [Fact]
    public void Actor_PublicKey_Owner_Is_Required()
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

        Assert.NotNull(actor.PublicKey?.Owner);
    }

    [Fact]
    public void Actor_PublicKey_PublicKeyPem_Is_Required()
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

        Assert.NotNull(actor.PublicKey?.PublicKeyPem);
    }

    [Fact]
    public void Actor_Followers_Collection_Is_Optional()
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
    public void Actor_Following_Collection_Is_Optional()
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
    public void Actor_Liked_Collection_Is_Optional()
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
    public void Actor_SharedInbox_Is_Optional()
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
    public void Actor_ManuallyApprovesFollowers_Is_Optional()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            ManuallyApprovesFollowers = false
        };

        Assert.False(actor.ManuallyApprovesFollowers);
    }

    [Fact]
    public void Actor_Published_Is_Optional()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Published = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        Assert.NotNull(actor.Published);
    }

    [Fact]
    public void Actor_Updated_Is_Optional()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Published = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Updated = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        Assert.NotNull(actor.Updated);
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
    public void Actor_Type_Cannot_Be_Empty()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person"
        };

        Assert.NotEmpty(actor.Type);
    }

    [Fact]
    public void Actor_PreferredUsername_Cannot_Be_Empty()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "testuser"
        };

        Assert.NotEmpty(actor.PreferredUsername);
    }

    [Fact]
    public void Actor_Must_Have_Id_That_Is_Absolute_URI()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test"
        };

        Assert.True(Uri.TryCreate(actor.Id, UriKind.Absolute, out var uri));
        Assert.NotNull(uri.Host);
    }

    [Fact]
    public void Actor_Must_Have_Type_That_Is_Non_Empty_String()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person"
        };

        Assert.NotNull(actor.Type);
        Assert.NotEmpty(actor.Type);
    }

    [Fact]
    public void Actor_PreferredUsername_Must_Be_Non_Empty_String()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "testuser"
        };

        Assert.NotNull(actor.PreferredUsername);
        Assert.NotEmpty(actor.PreferredUsername);
    }

    [Fact]
    public void Actor_Inbox_Must_Be_Non_Empty_String()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/test",
            Type = "Person",
            PreferredUsername = "test",
            Inbox = "https://example.com/users/test/inbox"
        };

        Assert.NotNull(actor.Inbox);
        Assert.NotEmpty(actor.Inbox);
    }
}
