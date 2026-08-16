using ActivityPub.Core.Events;
using ActivityPub.Core.Implementations;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ActivityPub.Tests;

public class InboxProcessorTests
{
    private readonly Mock<IActivityPubRepository> _repositoryMock;
    private readonly Mock<ILogger<InboxProcessor>> _loggerMock;
    private readonly InboxProcessor _inboxProcessor;

    public InboxProcessorTests()
    {
        _repositoryMock = new Mock<IActivityPubRepository>();
        _loggerMock = new Mock<ILogger<InboxProcessor>>();
        _inboxProcessor = new InboxProcessor(_repositoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task HandleEventAsync_ValidActivityReceivedEvent_ProcessesActivity()
    {
        // Arrange
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Create",
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/notes/456"
        };

        var @event = new ActivityReceivedEvent(activity);

        // Act
        await _inboxProcessor.HandleEventAsync(@event);

        // Assert
        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_NonActivityReceivedEvent_DoesNotProcess()
    {
        // Arrange
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Create",
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/notes/456"
        };

        var @event = new ActivityPublishedEvent(activity);

        // Act
        await _inboxProcessor.HandleEventAsync(@event);

        // Assert
        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Never);
    }

    [Fact]
    public async Task HandleEventAsync_NullActivity_LogsWarning()
    {
        // Arrange
        var @event = new ActivityReceivedEvent(null!);

        // Act
        await _inboxProcessor.HandleEventAsync(@event);

        // Assert
        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Never);
    }

    [Fact]
    public async Task HandleEventAsync_InvalidActivityWithoutId_Throws()
    {
        // Arrange
        var activity = new Activity
        {
            Id = string.Empty,
            Type = "Create",
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/notes/456"
        };

        var @event = new ActivityReceivedEvent(activity);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => _inboxProcessor.HandleEventAsync(@event));
    }

    [Fact]
    public async Task HandleEventAsync_InvalidActivityWithoutType_Throws()
    {
        // Arrange
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = string.Empty,
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/notes/456"
        };

        var @event = new ActivityReceivedEvent(activity);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => _inboxProcessor.HandleEventAsync(@event));
    }

    [Fact]
    public async Task HandleEventAsync_CreateActivityWithNote_ProcessesCorrectly()
    {
        // Arrange
        var note = new Note
        {
            Id = "https://example.com/notes/456",
            Type = "Note",
            Content = "Hello, World!"
        };

        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Create",
            Actor = "https://example.com/users/alice",
            Object = note
        };

        var @event = new ActivityReceivedEvent(activity);

        // Act
        await _inboxProcessor.HandleEventAsync(@event);

        // Assert
        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_FollowActivity_ProcessesCorrectly()
    {
        // Arrange
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Follow",
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/users/bob"
        };

        var @event = new ActivityReceivedEvent(activity);

        // Act
        await _inboxProcessor.HandleEventAsync(@event);

        // Assert
        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_LikeActivity_ProcessesCorrectly()
    {
        // Arrange
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Like",
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/notes/456"
        };

        var @event = new ActivityReceivedEvent(activity);

        // Act
        await _inboxProcessor.HandleEventAsync(@event);

        // Assert
        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_AnnounceActivity_ProcessesCorrectly()
    {
        // Arrange
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Announce",
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/notes/456"
        };

        var @event = new ActivityReceivedEvent(activity);

        // Act
        await _inboxProcessor.HandleEventAsync(@event);

        // Assert
        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_UndoActivity_ProcessesCorrectly()
    {
        // Arrange
        var followActivity = new Activity
        {
            Id = "https://example.com/activities/456",
            Type = "Follow",
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/users/bob"
        };

        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Undo",
            Actor = "https://example.com/users/alice",
            Object = followActivity
        };

        var @event = new ActivityReceivedEvent(activity);

        // Act
        await _inboxProcessor.HandleEventAsync(@event);

        // Assert
        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Once);
    }
}
