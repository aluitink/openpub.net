using ActivityPub.Core;
using Xunit;

namespace ActivityPub.Tests.Handlers;

public class ActivityHandlerFactoryTests
{
    private readonly ActivityHandlerFactory _factory;

    public ActivityHandlerFactoryTests()
    {
        _factory = new ActivityHandlerFactory();
    }

    [Fact]
    public void GetHandler_Create_ReturnsHandler()
    {
        var handler = _factory.GetHandler("Create");

        Assert.NotNull(handler);
        Assert.Equal("Create", handler!.ActivityType);
    }

    [Fact]
    public void GetHandler_Follow_ReturnsHandler()
    {
        var handler = _factory.GetHandler("Follow");

        Assert.NotNull(handler);
        Assert.Equal("Follow", handler!.ActivityType);
    }

    [Fact]
    public void GetHandler_Like_ReturnsHandler()
    {
        var handler = _factory.GetHandler("Like");

        Assert.NotNull(handler);
        Assert.Equal("Like", handler!.ActivityType);
    }

    [Fact]
    public void GetHandler_Announce_ReturnsHandler()
    {
        var handler = _factory.GetHandler("Announce");

        Assert.NotNull(handler);
        Assert.Equal("Announce", handler!.ActivityType);
    }

    [Fact]
    public void GetHandler_Undo_ReturnsHandler()
    {
        var handler = _factory.GetHandler("Undo");

        Assert.NotNull(handler);
        Assert.Equal("Undo", handler!.ActivityType);
    }

    [Fact]
    public void GetHandler_Delete_ReturnsHandler()
    {
        var handler = _factory.GetHandler("Delete");

        Assert.NotNull(handler);
        Assert.Equal("Delete", handler!.ActivityType);
    }

    [Fact]
    public void GetHandler_Update_ReturnsHandler()
    {
        var handler = _factory.GetHandler("Update");

        Assert.NotNull(handler);
        Assert.Equal("Update", handler!.ActivityType);
    }

    [Fact]
    public void GetHandler_Accept_ReturnsHandler()
    {
        var handler = _factory.GetHandler("Accept");

        Assert.NotNull(handler);
        Assert.Equal("Accept", handler!.ActivityType);
    }

    [Fact]
    public void GetHandler_Reject_ReturnsHandler()
    {
        var handler = _factory.GetHandler("Reject");

        Assert.NotNull(handler);
        Assert.Equal("Reject", handler!.ActivityType);
    }

    [Fact]
    public void GetHandler_Unknown_ReturnsNull()
    {
        var handler = _factory.GetHandler("Unknown");

        Assert.Null(handler);
    }
}
