using ActivityPub.Core;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ActivityPub.Tests.Handlers;

public class ActivityHandlersTests
{
    private readonly Mock<IActivityPubRepository> _repositoryMock;
    private readonly Mock<ILogger> _loggerMock;
    private readonly CreateActivityHandler _createHandler;
    private readonly FollowActivityHandler _followHandler;
    private readonly LikeActivityHandler _likeHandler;
    private readonly AnnounceActivityHandler _announceHandler;
    private readonly UndoActivityHandler _undoHandler;
    private readonly DeleteActivityHandler _deleteHandler;
    private readonly UpdateActivityHandler _updateHandler;

    public ActivityHandlersTests()
    {
        _repositoryMock = new Mock<IActivityPubRepository>();
        _loggerMock = new Mock<ILogger>();
        _createHandler = new CreateActivityHandler();
        _followHandler = new FollowActivityHandler();
        _likeHandler = new LikeActivityHandler();
        _announceHandler = new AnnounceActivityHandler();
        _undoHandler = new UndoActivityHandler();
        _deleteHandler = new DeleteActivityHandler();
        _updateHandler = new UpdateActivityHandler();
    }

    [Fact]
    public async Task CreateActivityHandler_HandleAsync_SavesActivity()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Create",
            Actor = "https://example.com/users/alice",
            Object = (object)new Note { Id = "https://example.com/notes/456", Type = "Note" }
        };

        await _createHandler.HandleAsync(activity, _repositoryMock.Object, _loggerMock.Object);

        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Once);
    }

    [Fact]
    public async Task CreateActivityHandler_HandleAsync_InvalidObject_DoesNotSave()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Create",
            Actor = "https://example.com/users/alice",
            Object = (object)"not-a-note"
        };

        await _createHandler.HandleAsync(activity, _repositoryMock.Object, _loggerMock.Object);

        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Never);
    }

    [Fact]
    public async Task FollowActivityHandler_HandleAsync_SavesActivity()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Follow",
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/users/bob"
        };

        await _followHandler.HandleAsync(activity, _repositoryMock.Object, _loggerMock.Object);

        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Once);
    }

    [Fact]
    public async Task FollowActivityHandler_HandleAsync_MissingIds_LogsWarning()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Follow",
            Actor = "",
            Object = ""
        };

        await _followHandler.HandleAsync(activity, _repositoryMock.Object, _loggerMock.Object);

        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Never);
    }

    [Fact]
    public async Task LikeActivityHandler_HandleAsync_SavesActivity()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Like",
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/notes/456"
        };

        await _likeHandler.HandleAsync(activity, _repositoryMock.Object, _loggerMock.Object);

        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Once);
    }

    [Fact]
    public async Task AnnounceActivityHandler_HandleAsync_SavesActivity()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Announce",
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/notes/456"
        };

        await _announceHandler.HandleAsync(activity, _repositoryMock.Object, _loggerMock.Object);

        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Once);
    }

    [Fact]
    public async Task UndoActivityHandler_HandleAsync_SavesActivity()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Undo",
            Actor = (object)new Actor { Id = "https://example.com/users/alice" },
            Object = (object)new Activity { Id = "https://example.com/activities/456", Type = "Create" }
        };

        await _undoHandler.HandleAsync(activity, _repositoryMock.Object, _loggerMock.Object);

        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Once);
    }

    [Fact]
    public async Task DeleteActivityHandler_HandleAsync_SavesActivity()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Delete",
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/notes/456"
        };

        await _deleteHandler.HandleAsync(activity, _repositoryMock.Object, _loggerMock.Object);

        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Once);
    }

    [Fact]
    public async Task UpdateActivityHandler_HandleAsync_SavesActivity()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Update",
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/notes/456"
        };

        await _updateHandler.HandleAsync(activity, _repositoryMock.Object, _loggerMock.Object);

        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Once);
    }
}
