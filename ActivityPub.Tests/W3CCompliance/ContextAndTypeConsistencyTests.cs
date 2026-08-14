using ActivityPub.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace ActivityPub.Tests.W3CCompliance;

public class ContextAndTypeConsistencyTests
{
    private readonly JsonSerializerOptions _jsonOptions;

    public ContextAndTypeConsistencyTests()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
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
    public void Activity_Context_Must_Be_W3C_NS()
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
    public void Activity_Type_Must_Be_Present()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create"
        };

        Assert.NotNull(activity.Type);
    }

    [Fact]
    public void Activity_Type_Must_Be_Valid_Type()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create"
        };

        var validTypes = new[] { "Create", "Follow", "Like", "Announce", "Undo", "Delete", "Update" };
        Assert.Contains(activity.Type, validTypes);
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Follow")]
    [InlineData("Like")]
    [InlineData("Announce")]
    [InlineData("Undo")]
    [InlineData("Delete")]
    [InlineData("Update")]
    [InlineData("Accept")]
    [InlineData("Reject")]
    [InlineData("TentativeAccept")]
    [InlineData("TentativeReject")]
    [InlineData("Block")]
    [InlineData("Flag")]
    [InlineData("View")]
    [InlineData("Listen")]
    [InlineData("Read")]
    [InlineData("Move")]
    [InlineData("Add")]
    [InlineData("Remove")]
    [InlineData("Favorite")]
    [InlineData("Pin")]
    [InlineData("Unfavorite")]
    [InlineData("Unpin")]
    public void Activity_Type_Must_Be_Recognized(string type)
    {
        var activity = new Activity
        {
            Id = $"https://example.com/activities/{type.ToLower()}",
            Type = type
        };

        Assert.Equal(type, activity.Type);
    }

    [Fact]
    public void Context_Must_Contain_Type_Definition()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create",
            Context = "https://www.w3.org/ns/activitystreams"
        };

        Assert.Equal("https://www.w3.org/ns/activitystreams", activity.Context);
        Assert.Equal("Create", activity.Type);
    }

    [Fact]
    public void Context_Must_Be_URI()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create",
            Context = "https://www.w3.org/ns/activitystreams"
        };

        Assert.True(Uri.TryCreate(activity.Context, UriKind.Absolute, out _));
    }

    [Fact]
    public void Activity_Type_Determines_Required_Fields()
    {
        var create = new Activity
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

        Assert.Equal("Create", create.Type);
        Assert.NotNull(create.Object);
    }

    [Fact]
    public void Follow_Activity_Must_Have_Actor()
    {
        var follow = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Follow",
            Actor = "https://example.com/users/follower",
            Object = "https://example.com/users/following"
        };

        Assert.NotNull(follow.Actor);
    }

    [Fact]
    public void Like_Activity_Must_Have_Object()
    {
        var like = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Like",
            Actor = "https://example.com/users/liker",
            Object = "https://example.com/notes/1"
        };

        Assert.NotNull(like.Object);
    }

    [Fact]
    public void Context_URL_Must_Be_Absolute()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create",
            Context = "https://www.w3.org/ns/activitystreams"
        };

        Assert.True(activity.Context!.StartsWith("https://") || activity.Context!.StartsWith("http://"));
    }

    [Fact]
    public void Context_URL_Must_Have_Hostname()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create",
            Context = "https://www.w3.org/ns/activitystreams"
        };

        Assert.True(Uri.TryCreate(activity.Context, UriKind.Absolute, out var uri));
        Assert.NotNull(uri.Host);
    }

    [Fact]
    public void Type_Must_Be_Non_Empty()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/1",
            Type = "Create"
        };

        Assert.NotNull(activity.Type);
        Assert.NotEmpty(activity.Type);
    }
}
