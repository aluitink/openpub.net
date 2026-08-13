using ActivityPub.Core.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ActivityPub.Tests.Services;

public class ActivityValidationServiceTests
{
    private readonly ActivityValidationService _service;

    public ActivityValidationServiceTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<ActivityValidationService>();
        _service = new ActivityValidationService(logger);
    }

    [Fact]
    public void Validate_NullActivity_ReturnsFalse()
    {
        var result = _service.Validate(null!, out var errors);

        Assert.False(result);
        Assert.Contains("Activity JSON is null or empty", errors);
    }

    [Fact]
    public void Validate_EmptyActivity_ReturnsFalse()
    {
        var result = _service.Validate("", out var errors);

        Assert.False(result);
        Assert.Contains("Activity JSON is null or empty", errors);
    }

    [Fact]
    public void Validate_InvalidJson_ReturnsFalse()
    {
        var result = _service.Validate("{invalid json}", out var errors);

        Assert.False(result);
    }

    [Fact]
    public void Validate_ValidActivity_ReturnsTrue()
    {
        var activity = """
        {
            "type": "Create",
            "id": "https://example.com/activities/123",
            "actor": {
                "id": "https://example.com/users/test",
                "type": "Person"
            },
            "object": "https://example.com/objects/456",
            "published": "2024-01-01T00:00:00Z"
        }
        """;

        var result = _service.Validate(activity, out var errors);

        Assert.True(result);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MissingType_ReturnsFalse()
    {
        var activity = """
        {
            "id": "https://example.com/activities/123",
            "actor": {
                "id": "https://example.com/users/test",
                "type": "Person"
            },
            "object": "https://example.com/objects/456"
        }
        """;

        var result = _service.Validate(activity, out var errors);

        Assert.False(result);
        Assert.Contains("Activity type is required", errors);
    }

    [Fact]
    public void Validate_MissingActor_ReturnsFalse()
    {
        var activity = """
        {
            "type": "Create",
            "id": "https://example.com/activities/123",
            "object": "https://example.com/objects/456"
        }
        """;

        var result = _service.Validate(activity, out var errors);

        Assert.False(result);
        Assert.Contains("Actor is required", errors);
    }
}
