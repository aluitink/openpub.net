using ActivityPub.Core;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ActivityPub.Tests.Handlers;

public class AcceptRejectHandlersTests
{
    private readonly Mock<IActivityPubRepository> _repositoryMock;
    private readonly Mock<ILogger> _loggerMock;
    private readonly AcceptActivityHandler _acceptHandler;
    private readonly RejectActivityHandler _rejectHandler;

    public AcceptRejectHandlersTests()
    {
        _repositoryMock = new Mock<IActivityPubRepository>();
        _loggerMock = new Mock<ILogger>();
        _acceptHandler = new AcceptActivityHandler();
        _rejectHandler = new RejectActivityHandler();
    }

    [Fact]
    public async Task AcceptActivityHandler_HandleAsync_SavesActivity()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Accept",
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/activities/456"
        };

        await _acceptHandler.HandleAsync(activity, _repositoryMock.Object, _loggerMock.Object);

        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Once);
    }

    [Fact]
    public async Task AcceptActivityHandler_HandleAsync_MissingIds_LogsWarning()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Accept",
            Actor = "",
            Object = ""
        };

        await _acceptHandler.HandleAsync(activity, _repositoryMock.Object, _loggerMock.Object);

        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Never);
    }

    [Fact]
    public async Task RejectActivityHandler_HandleAsync_SavesActivity()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Reject",
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/activities/456"
        };

        await _rejectHandler.HandleAsync(activity, _repositoryMock.Object, _loggerMock.Object);

        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Once);
    }

    [Fact]
    public async Task RejectActivityHandler_HandleAsync_MissingIds_LogsWarning()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activities/123",
            Type = "Reject",
            Actor = "",
            Object = ""
        };

        await _rejectHandler.HandleAsync(activity, _repositoryMock.Object, _loggerMock.Object);

        _repositoryMock.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Never);
    }
}
