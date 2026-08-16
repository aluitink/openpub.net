using ActivityPub.Core.Services;
using Moq;
using Xunit;

namespace ActivityPub.Tests.Services;

public class WebSubServiceTests
{
    [Fact]
    public void VerifySubscriptionAsync_SubscribeModeReturnsChallenge()
    {
        var options = new WebSubOptions();
        var service = new WebSubService(new Mock<IHttpClientFactory>().Object, options);

        var result = service.VerifySubscriptionAsync("subscribe", "https://example.com/actor", "3600", "test-challenge", "https://callback.example.com/websub");

        Assert.Equal("test-challenge", result);
    }

    [Fact]
    public void VerifySubscriptionAsync_UnsubscribeModeReturnsChallenge()
    {
        var options = new WebSubOptions();
        var service = new WebSubService(new Mock<IHttpClientFactory>().Object, options);

        var result = service.VerifySubscriptionAsync("unsubscribe", "https://example.com/actor", "3600", "test-challenge", "https://callback.example.com/websub");

        Assert.Equal("test-challenge", result);
    }

    [Fact]
    public void VerifySubscriptionAsync_InvalidModeThrows()
    {
        var options = new WebSubOptions();
        var service = new WebSubService(new Mock<IHttpClientFactory>().Object, options);

        Assert.Throws<InvalidOperationException>(() => service.VerifySubscriptionAsync("invalid", "https://example.com/actor", "3600", "test-challenge", "https://callback.example.com/websub"));
    }

    [Fact]
    public void VerifySubscriptionAsync_NullModeThrows()
    {
        var options = new WebSubOptions();
        var service = new WebSubService(new Mock<IHttpClientFactory>().Object, options);

        Assert.Throws<ArgumentException>(() => service.VerifySubscriptionAsync("", "https://example.com/actor", "3600", "test-challenge", "https://callback.example.com/websub"));
    }

    [Fact]
    public void VerifySubscriptionAsync_NullTopicThrows()
    {
        var options = new WebSubOptions();
        var service = new WebSubService(new Mock<IHttpClientFactory>().Object, options);

        Assert.Throws<ArgumentException>(() => service.VerifySubscriptionAsync("subscribe", "", "3600", "test-challenge", "https://callback.example.com/websub"));
    }

    [Fact]
    public void VerifySubscriptionAsync_NullChallengeThrows()
    {
        var options = new WebSubOptions();
        var service = new WebSubService(new Mock<IHttpClientFactory>().Object, options);

        Assert.Throws<ArgumentException>(() => service.VerifySubscriptionAsync("subscribe", "https://example.com/actor", "3600", "", "https://callback.example.com/websub"));
    }

    [Fact]
    public void VerifySubscriptionAsync_NullCallbackUrlThrows()
    {
        var options = new WebSubOptions();
        var service = new WebSubService(new Mock<IHttpClientFactory>().Object, options);

        Assert.Throws<ArgumentException>(() => service.VerifySubscriptionAsync("subscribe", "https://example.com/actor", "3600", "test-challenge", ""));
    }
}
