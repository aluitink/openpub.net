using ActivityPub.Core.Events;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ActivityPub.Tests.Services;

public class ActivityPubServiceTests
{
    private readonly Mock<IActivityPubRepository> _repositoryMock;
    private readonly Mock<ILogger<ActivityPubService>> _loggerMock;
    private readonly ActivityPub.Core.Services.ActivityPubEventDispatcher _eventDispatcher;
    private readonly ActivityPubService _service;

    public ActivityPubServiceTests()
    {
        _repositoryMock = new Mock<IActivityPubRepository>();
        _loggerMock = new Mock<ILogger<ActivityPubService>>();
        _eventDispatcher = new ActivityPub.Core.Services.ActivityPubEventDispatcher();
        _service = new ActivityPubService(_repositoryMock.Object, _eventDispatcher, Enumerable.Empty<IActivityPubInterceptor>(), _loggerMock.Object);
    }

    [Fact]
    public async Task GetActorWithEventAsync_ExistingActor_ReturnsActor()
    {
        var expectedActor = new Actor { Id = "https://example.com/users/alice", PreferredUsername = "alice" };
        _repositoryMock.Setup(r => r.GetUserActorAsync("alice")).ReturnsAsync(expectedActor);

        var result = await _service.GetActorWithEventAsync("alice");

        Assert.Equal("alice", result?.PreferredUsername);
        _repositoryMock.Verify(r => r.GetUserActorAsync("alice"), Times.Once);
    }

    [Fact]
    public async Task GetActorWithEventAsync_NonExistingActor_ReturnsNull()
    {
        _repositoryMock.Setup(r => r.GetUserActorAsync("alice")).ReturnsAsync((Actor?)null);

        var result = await _service.GetActorWithEventAsync("alice");

        Assert.Null(result);
        _repositoryMock.Verify(r => r.GetUserActorAsync("alice"), Times.Once);
    }

    [Fact]
    public async Task GetActorWithEventAsync_Throws_Exception_Rethrows()
    {
        _repositoryMock.Setup(r => r.GetUserActorAsync("alice")).ThrowsAsync(new Exception("Test error"));

        await Assert.ThrowsAsync<Exception>(() => _service.GetActorWithEventAsync("alice"));
    }

    [Fact]
    public async Task ProcessIncomingActivityAsync_ValidActivity_ReturnsTrue()
    {
        var activity = new Activity { Id = "https://example.com/activities/123", Type = "Create", Actor = "https://example.com/users/alice", Object = "https://example.com/notes/456" };

        var result = await _service.ProcessIncomingActivityAsync(activity);

        Assert.True(result);
        _repositoryMock.Verify(r => r.SaveUserActorAsync(It.IsAny<Actor>()), Times.Never);
    }

    [Fact]
    public async Task ProcessIncomingActivityAsync_WithInterceptor_CallsInterceptor()
    {
        var activity = new Activity { Id = "https://example.com/activities/123", Type = "Create", Actor = "https://example.com/users/alice", Object = "https://example.com/notes/456" };
        var interceptorMock = new Mock<IActivityPubInterceptor>();
        interceptorMock.Setup(i => i.OnActivityReceivedAsync(It.IsAny<Activity>())).ReturnsAsync(true);

        var service = new ActivityPubService(_repositoryMock.Object, _eventDispatcher, new[] { interceptorMock.Object }, _loggerMock.Object);

        var result = await service.ProcessIncomingActivityAsync(activity);

        Assert.True(result);
        interceptorMock.Verify(i => i.OnActivityReceivedAsync(activity), Times.Once);
    }

    [Fact]
    public async Task ProcessIncomingActivityAsync_InterceptorStopsProcessing_ReturnsFalse()
    {
        var activity = new Activity { Id = "https://example.com/activities/123", Type = "Create", Actor = "https://example.com/users/alice", Object = "https://example.com/notes/456" };
        var interceptorMock = new Mock<IActivityPubInterceptor>();
        interceptorMock.Setup(i => i.OnActivityReceivedAsync(It.IsAny<Activity>())).ReturnsAsync(false);

        var service = new ActivityPubService(_repositoryMock.Object, _eventDispatcher, new[] { interceptorMock.Object }, _loggerMock.Object);

        var result = await service.ProcessIncomingActivityAsync(activity);

        Assert.False(result);
    }

    [Fact]
    public async Task ProcessIncomingActivityAsync_Throws_Exception_Rethrows()
    {
        _repositoryMock.Setup(r => r.GetUserActorAsync("alice")).ThrowsAsync(new Exception("Test error"));

        await Assert.ThrowsAsync<Exception>(() => _service.GetActorWithEventAsync("alice"));
    }
}
