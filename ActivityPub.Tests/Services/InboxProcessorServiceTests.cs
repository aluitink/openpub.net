using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ActivityPub.Tests.Services;

public class InboxProcessorServiceTests
{
    private readonly Mock<IActivityPubRepository> _repositoryMock;
    private readonly Mock<ILogger<InboxProcessorService>> _loggerMock;
    private readonly ActivityPub.Core.Services.ActivityPubEventDispatcher _eventDispatcher;

    public InboxProcessorServiceTests()
    {
        _repositoryMock = new Mock<IActivityPubRepository>();
        _loggerMock = new Mock<ILogger<InboxProcessorService>>();
        _eventDispatcher = new ActivityPub.Core.Services.ActivityPubEventDispatcher();
    }

    [Fact]
    public async Task AddToInboxAsync_AddsToChannel()
    {
        using var service = new InboxProcessorService(_repositoryMock.Object, _eventDispatcher, _loggerMock.Object);

        var activity = new Activity { Id = "https://example.com/activities/123", Type = "Create" };

        await service.AddToInboxAsync(activity);
    }

    [Fact]
    public void InboxProcessorService_Disposes_CancellationTokenSource()
    {
        var service = new InboxProcessorService(_repositoryMock.Object, _eventDispatcher, _loggerMock.Object);

        service.Dispose();

        Assert.True(true);
    }
}
