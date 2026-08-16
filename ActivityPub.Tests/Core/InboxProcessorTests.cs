using ActivityPub.Core.Events;
using ActivityPub.Core.Implementations;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace ActivityPub.Tests.Core;

public class InboxProcessorTests
{
    [Fact]
    public async Task ProcessDeleteActivity_DeletesOriginalActivity()
    {
        var activity = CreateActivity("Delete", "actor1", "obj123");
        var originalActivity = CreateActivity("Create", "actor1", "obj123");
        originalActivity.Id = "obj123";

        var repo = new Mock<IActivityPubRepository>();
        repo.Setup(r => r.GetActivityAsync("obj123")).ReturnsAsync(originalActivity);
        repo.Setup(r => r.DeleteActivityAsync("obj123")).ReturnsAsync(true);
        repo.Setup(r => r.SaveActivityAsync(It.IsAny<Activity>())).ReturnsAsync(true);

        var logger = new Mock<ILogger<InboxProcessor>>();
        var processor = new InboxProcessor(repo.Object, logger.Object);

        var evt = new ActivityReceivedEvent(activity);
        await processor.HandleEventAsync(evt);

        repo.Verify(r => r.DeleteActivityAsync("obj123"), Times.Once);
    }

    [Fact]
    public async Task ProcessDeleteActivity_IgnoresMissingOriginal()
    {
        var activity = CreateActivity("Delete", "actor1", "nonexistent");

        var repo = new Mock<IActivityPubRepository>();
        repo.Setup(r => r.GetActivityAsync("nonexistent")).ReturnsAsync((Activity?)null);
        repo.Setup(r => r.SaveActivityAsync(It.IsAny<Activity>())).ReturnsAsync(true);

        var logger = new Mock<ILogger<InboxProcessor>>();
        var processor = new InboxProcessor(repo.Object, logger.Object);

        var evt = new ActivityReceivedEvent(activity);
        await processor.HandleEventAsync(evt);

        repo.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Once);
        repo.Verify(r => r.DeleteActivityAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessUpdateActivity_SavesActivity()
    {
        var activity = CreateActivity("Update", "actor1", "note456");

        var repo = new Mock<IActivityPubRepository>();
        repo.Setup(r => r.SaveActivityAsync(It.IsAny<Activity>())).ReturnsAsync(true);

        var logger = new Mock<ILogger<InboxProcessor>>();
        var processor = new InboxProcessor(repo.Object, logger.Object);

        var evt = new ActivityReceivedEvent(activity);
        await processor.HandleEventAsync(evt);

        repo.Verify(r => r.SaveActivityAsync(activity), Times.Once);
    }

    [Fact]
    public async Task ProcessMoveActivity_SavesActivity()
    {
        var activity = CreateActivity("Move", "actor1", "obj789");
        activity.AdditionalProperties = new Dictionary<string, JsonElement>
        {
            ["target"] = JsonSerializer.SerializeToElement("https://newserver/users/newuser")
        };

        var repo = new Mock<IActivityPubRepository>();
        repo.Setup(r => r.SaveActivityAsync(It.IsAny<Activity>())).ReturnsAsync(true);

        var logger = new Mock<ILogger<InboxProcessor>>();
        var processor = new InboxProcessor(repo.Object, logger.Object);

        var evt = new ActivityReceivedEvent(activity);
        await processor.HandleEventAsync(evt);

        repo.Verify(r => r.SaveActivityAsync(activity), Times.Once);
    }

    static Activity CreateActivity(string type, string actorId, string objectId)
    {
        return new Activity
        {
            Id = $"https://localhost/activities/{Guid.NewGuid()}",
            Type = type,
            Actor = actorId,
            Object = objectId,
            Published = DateTime.UtcNow,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" }
        };
    }
}
