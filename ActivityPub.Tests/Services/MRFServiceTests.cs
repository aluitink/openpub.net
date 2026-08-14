using ActivityPub.Core.Models;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace ActivityPub.Tests.Services;

public class MRFServiceTests
{
    private readonly ILoggerFactory _loggerFactory;

    public MRFServiceTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    }

    [Fact]
    public async Task ProcessAsync_RejectsActivityWithFilteredContent()
    {
        var options = Options.Create(new ActivityPubOptions
        {
            MRFOptions = new MRFOptions
            {
                ProhibitedWords = new List<string> { "spam" },
                MaxContentLength = 1000
            }
        });

        var logger = _loggerFactory.CreateLogger<MRFService>();
        var service = new MRFService(options, logger);

        var activity = new Activity
        {
            Id = "https://example.com/activity/1",
            Type = "Create",
            Object = new ActivityPub.Core.Models.Object
            {
                Id = "https://example.com/object/1",
                Type = "Note",
                Content = "This is spam content"
            }
        };

        var result = await service.ProcessAsync(activity);

        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessAsync_RejectsActivityFromBlockedDomain()
    {
        var options = Options.Create(new ActivityPubOptions
        {
            MRFOptions = new MRFOptions
            {
                BlockedDomains = new List<string> { "badactor.com" }
            }
        });

        var logger = _loggerFactory.CreateLogger<MRFService>();
        var service = new MRFService(options, logger);

        var activity = new Activity
        {
            Id = "https://example.com/activity/1",
            Type = "Create",
            AttributedTo = "https://badactor.com/user",
            Object = new ActivityPub.Core.Models.Object
            {
                Id = "https://example.com/object/1",
                Type = "Note",
                Content = "Good content"
            }
        };

        var result = await service.ProcessAsync(activity);

        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessAsync_RejectsActivityOverContentLength()
    {
        var options = Options.Create(new ActivityPubOptions
        {
            MRFOptions = new MRFOptions
            {
                MaxContentLength = 10
            }
        });

        var logger = _loggerFactory.CreateLogger<MRFService>();
        var service = new MRFService(options, logger);

        var activity = new Activity
        {
            Id = "https://example.com/activity/1",
            Type = "Create",
            Object = new ActivityPub.Core.Models.Object
            {
                Id = "https://example.com/object/1",
                Type = "Note",
                Content = "This content is too long"
            }
        };

        var result = await service.ProcessAsync(activity);

        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessAsync_AllowsValidActivity()
    {
        var options = Options.Create(new ActivityPubOptions
        {
            MRFOptions = new MRFOptions
            {
                ProhibitedWords = new List<string> { "spam" },
                MaxContentLength = 1000
            }
        });

        var logger = _loggerFactory.CreateLogger<MRFService>();
        var service = new MRFService(options, logger);

        var activity = new Activity
        {
            Id = "https://example.com/activity/1",
            Type = "Create",
            Object = new ActivityPub.Core.Models.Object
            {
                Id = "https://example.com/object/1",
                Type = "Note",
                Content = "This is valid content"
            }
        };

        var result = await service.ProcessAsync(activity);

        Assert.NotNull(result);
        Assert.Equal("Create", result.Type);
    }

    [Fact]
    public async Task ProcessAsync_HandlesNullActivityType()
    {
        var options = Options.Create(new ActivityPubOptions
        {
            MRFOptions = new ActivityPub.Core.Options.MRFOptions()
        });

        var logger = _loggerFactory.CreateLogger<MRFService>();
        var service = new MRFService(options, logger);

        var activity = new Activity
        {
            Id = "https://example.com/activity/1",
            Type = null!,
            Object = new ActivityPub.Core.Models.Object
            {
                Id = "https://example.com/object/1",
                Type = "Note",
                Content = "test"
            }
        };

        var result = await service.ProcessAsync(activity);

        Assert.Null(result);
    }
}
