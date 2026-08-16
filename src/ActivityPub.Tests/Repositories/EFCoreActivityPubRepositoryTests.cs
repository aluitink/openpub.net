using ActivityPub.Core.Implementations;
using ActivityPub.Core.Models;
using ActivityPub.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace ActivityPub.Tests.Repositories;

public class EFCoreActivityPubRepositoryTests
{
    private ActivityPubDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ActivityPubDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ActivityPubDbContext(options);
    }

    [Fact]
    public async Task SaveUserActorAsync_SavesNewActor()
    {
        var context = CreateDbContext();
        var repository = new EFCoreActivityPubRepository(context);

        var actor = new Actor
        {
            Id = "https://example.com/users/testuser",
            Type = "Person",
            PreferredUsername = "testuser",
            Name = "Test User"
        };

        var result = await repository.SaveUserActorAsync(actor);

        Assert.True(result);
        Assert.Equal(1, await context.Actors.CountAsync());
    }

    [Fact]
    public async Task SaveUserActorAsync_UpdatesExistingActor()
    {
        var context = CreateDbContext();
        var repository = new EFCoreActivityPubRepository(context);

        var actor1 = new Actor
        {
            Id = "https://example.com/users/testuser",
            Type = "Person",
            PreferredUsername = "testuser",
            Name = "Test User"
        };

        await repository.SaveUserActorAsync(actor1);

        var actor2 = new Actor
        {
            Id = "https://example.com/users/testuser",
            Type = "Person",
            PreferredUsername = "testuser",
            Name = "Updated User"
        };

        var result = await repository.SaveUserActorAsync(actor2);

        Assert.True(result);
        Assert.Equal(1, await context.Actors.CountAsync());
        var savedEntity = await context.Actors.FirstAsync();
        var deserialized = JsonSerializer.Deserialize<Actor>(savedEntity.JsonData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal("Updated User", deserialized?.Name);
    }

    [Fact]
    public async Task GetUserActorAsync_ReturnsExistingActor()
    {
        var context = CreateDbContext();
        var repository = new EFCoreActivityPubRepository(context);

        var actor = new Actor
        {
            Id = "https://example.com/users/testuser",
            Type = "Person",
            PreferredUsername = "testuser",
            Name = "Test User"
        };

        await repository.SaveUserActorAsync(actor);

        var result = await repository.GetUserActorAsync("testuser");

        Assert.NotNull(result);
        Assert.Equal("testuser", result?.PreferredUsername);
        Assert.Equal("Test User", result?.Name);
    }

    [Fact]
    public async Task GetUserActorAsync_ReturnsNullForNonExistentUser()
    {
        var context = CreateDbContext();
        var repository = new EFCoreActivityPubRepository(context);

        var result = await repository.GetUserActorAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveActivityAsync_SavesNewActivity()
    {
        var context = CreateDbContext();
        var repository = new EFCoreActivityPubRepository(context);

        var note = new Note
        {
            Id = "https://example.com/notes/456",
            Type = "Note",
            Content = "Hello World"
        };

        var activity = new Create
        {
            Id = "https://example.com/activities/123",
            Type = "Create",
            Actor = "https://example.com/users/alice",
            Object = note
        };

        var result = await repository.SaveActivityAsync(activity);

        Assert.True(result);
        Assert.Equal(1, await context.Activities.CountAsync());
    }

    [Fact]
    public async Task SaveActivityAsync_UpdatesExistingActivity()
    {
        var context = CreateDbContext();
        var repository = new EFCoreActivityPubRepository(context);

        var note1 = new Note
        {
            Id = "https://example.com/notes/456",
            Type = "Note",
            Content = "First content"
        };

        var activity1 = new Create
        {
            Id = "https://example.com/activities/123",
            Type = "Create",
            Actor = "https://example.com/users/alice",
            Object = note1
        };

        await repository.SaveActivityAsync(activity1);

        var note2 = new Note
        {
            Id = "https://example.com/notes/456",
            Type = "Note",
            Content = "Updated content"
        };

        var activity2 = new Create
        {
            Id = "https://example.com/activities/123",
            Type = "Create",
            Actor = "https://example.com/users/alice",
            Object = note2
        };

        var result = await repository.SaveActivityAsync(activity2);

        Assert.True(result);
        Assert.Equal(1, await context.Activities.CountAsync());
        var savedEntity = await context.Activities.FirstAsync();
        var deserialized = JsonSerializer.Deserialize<Create>(savedEntity.JsonData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Contains("Updated content", JsonSerializer.Serialize(deserialized?.Object));
    }

    [Fact]
    public async Task GetActivityAsync_ReturnsExistingActivity()
    {
        var context = CreateDbContext();
        var repository = new EFCoreActivityPubRepository(context);

        var note = new Note
        {
            Id = "https://example.com/notes/456",
            Type = "Note",
            Content = "Hello World"
        };

        var activity = new Create
        {
            Id = "https://example.com/activities/123",
            Type = "Create",
            Actor = "https://example.com/users/alice",
            Object = note
        };

        await repository.SaveActivityAsync(activity);

        var result = await repository.GetActivityAsync("https://example.com/activities/123");

        Assert.NotNull(result);
        Assert.Equal("Create", result?.Type);
    }

    [Fact]
    public async Task GetActivityAsync_ReturnsNullForNonExistentActivity()
    {
        var context = CreateDbContext();
        var repository = new EFCoreActivityPubRepository(context);

        var result = await repository.GetActivityAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActorOutboxActivitiesAsync_ReturnsActivityIds()
    {
        var context = CreateDbContext();
        var repository = new EFCoreActivityPubRepository(context);

        var actor = new Actor
        {
            Id = "https://example.com/users/testuser",
            Type = "Person",
            PreferredUsername = "testuser"
        };

        await repository.SaveUserActorAsync(actor);

        var activities = new List<Activity>
        {
            new Create { Id = "https://example.com/activities/1", Actor = actor.Id, Type = "Create", Published = DateTime.UtcNow.AddDays(-2) },
            new Create { Id = "https://example.com/activities/2", Actor = actor.Id, Type = "Create", Published = DateTime.UtcNow.AddDays(-1) },
            new Create { Id = "https://example.com/activities/3", Actor = actor.Id, Type = "Create", Published = DateTime.UtcNow }
        };

        foreach (var activity in activities)
        {
            await repository.SaveActivityAsync(activity);
        }

        var result = await repository.GetActorOutboxActivitiesAsync("testuser", 0, 10);

        Assert.Equal(3, result.Count);
        Assert.Contains("https://example.com/activities/1", result);
        Assert.Contains("https://example.com/activities/2", result);
        Assert.Contains("https://example.com/activities/3", result);
    }

    [Fact]
    public async Task GetActorOutboxActivitiesAsync_RespectsSkipAndLimit()
    {
        var context = CreateDbContext();
        var repository = new EFCoreActivityPubRepository(context);

        var actor = new Actor
        {
            Id = "https://example.com/users/testuser",
            Type = "Person",
            PreferredUsername = "testuser"
        };

        await repository.SaveUserActorAsync(actor);

        for (int i = 1; i <= 5; i++)
        {
            await repository.SaveActivityAsync(new Create
            {
                Id = $"https://example.com/activities/{i}",
                Actor = actor.Id,
                Type = "Create",
                Published = DateTime.UtcNow.AddDays(-i)
            });
        }

        var result = await repository.GetActorOutboxActivitiesAsync("testuser", 1, 2);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task DeleteActivityAsync_DeletesExistingActivity()
    {
        var context = CreateDbContext();
        var repository = new EFCoreActivityPubRepository(context);

        var note = new Note
        {
            Id = "https://example.com/notes/456",
            Type = "Note",
            Content = "Hello World"
        };

        var activity = new Create
        {
            Id = "https://example.com/activities/123",
            Type = "Create",
            Actor = "https://example.com/users/alice",
            Object = note
        };

        await repository.SaveActivityAsync(activity);

        var result = await repository.DeleteActivityAsync("https://example.com/activities/123");

        Assert.True(result);
        Assert.Equal(0, await context.Activities.CountAsync());
    }

    [Fact]
    public async Task DeleteActivityAsync_ReturnsFalseForNonExistentActivity()
    {
        var context = CreateDbContext();
        var repository = new EFCoreActivityPubRepository(context);

        var result = await repository.DeleteActivityAsync("nonexistent");

        Assert.False(result);
    }

    [Fact]
    public async Task GetUserActorAsync_UsesPreferredUsername()
    {
        var context = CreateDbContext();
        var repository = new EFCoreActivityPubRepository(context);

        var actor = new Actor
        {
            Id = "https://example.com/users/testuser",
            Type = "Person",
            PreferredUsername = "testuser",
            Name = "Test User"
        };

        await repository.SaveUserActorAsync(actor);

        var result = await repository.GetUserActorAsync("testuser");

        Assert.NotNull(result);
        Assert.Equal("testuser", result?.PreferredUsername);
    }

    [Fact]
    public async Task GetUserActorAsync_FallsBackToId()
    {
        var context = CreateDbContext();
        var repository = new EFCoreActivityPubRepository(context);

        var actor = new Actor
        {
            Id = "https://example.com/users/testuser",
            Type = "Person",
            Name = "Test User"
        };

        await repository.SaveUserActorAsync(actor);

        var result = await repository.GetUserActorAsync("testuser");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetActorOutboxActivitiesAsync_ReturnsEmptyForNonExistentUser()
    {
        var context = CreateDbContext();
        var repository = new EFCoreActivityPubRepository(context);

        var result = await repository.GetActorOutboxActivitiesAsync("nonexistent", 0, 10);

        Assert.Empty(result);
    }

    [Fact]
    public async Task SaveUserActorAsync_CorrectlySerializesJson()
    {
        var context = CreateDbContext();
        var repository = new EFCoreActivityPubRepository(context);

        var actor = new Actor
        {
            Id = "https://example.com/users/testuser",
            Type = "Person",
            PreferredUsername = "testuser",
            Name = "Test User",
            Inbox = "https://example.com/users/testuser/inbox"
        };

        await repository.SaveUserActorAsync(actor);

        var savedEntity = await context.Actors.FirstAsync();
        var deserialized = JsonSerializer.Deserialize<Actor>(savedEntity.JsonData, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(deserialized);
        Assert.Equal("Person", deserialized?.Type);
        Assert.Equal("testuser", deserialized?.PreferredUsername);
        Assert.Equal("Test User", deserialized?.Name);
    }

    [Fact]
    public async Task GetFollowersAsync_ReturnsFollowers()
    {
        var context = CreateDbContext();
        var repository = new EFCoreActivityPubRepository(context);

        var actor = new Actor
        {
            Id = "https://example.com/users/testuser",
            Type = "Person",
            PreferredUsername = "testuser"
        };

        await repository.SaveUserActorAsync(actor);

        await repository.SaveActivityAsync(new Follow
        {
            Id = "https://example.com/activities/follow1",
            Type = "Follow",
            Actor = "https://example.com/users/follower1",
            Object = actor.Id
        });

        await repository.SaveActivityAsync(new Follow
        {
            Id = "https://example.com/activities/follow2",
            Type = "Follow",
            Actor = "https://example.com/users/follower2",
            Object = actor.Id
        });

        var followers = await repository.GetFollowersAsync("testuser", 0, 10);

        Assert.Equal(2, followers.Count);
        Assert.Contains("https://example.com/users/follower1", followers);
        Assert.Contains("https://example.com/users/follower2", followers);
    }

    [Fact]
    public async Task GetFollowingAsync_ReturnsFollowing()
    {
        var context = CreateDbContext();
        var repository = new EFCoreActivityPubRepository(context);

        var actor = new Actor
        {
            Id = "https://example.com/users/testuser",
            Type = "Person",
            PreferredUsername = "testuser"
        };

        await repository.SaveUserActorAsync(actor);

        await repository.SaveActivityAsync(new Follow
        {
            Id = "https://example.com/activities/follow1",
            Type = "Follow",
            Actor = actor.Id,
            Object = "https://example.com/users/beingfollowed1"
        });

        await repository.SaveActivityAsync(new Follow
        {
            Id = "https://example.com/activities/follow2",
            Type = "Follow",
            Actor = actor.Id,
            Object = "https://example.com/users/beingfollowed2"
        });

        var following = await repository.GetFollowingAsync("testuser", 0, 10);

        Assert.Equal(2, following.Count);
        Assert.Contains("https://example.com/users/beingfollowed1", following);
        Assert.Contains("https://example.com/users/beingfollowed2", following);
    }
}
