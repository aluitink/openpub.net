using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using ActivityPub.Core.Models;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActivityPub.Tests.LoadTesting;

/// <summary>
/// Benchmarks ActivityPub activity validation using ActivityValidationService.
/// Measures validation of valid activities, invalid/malformed activities,
/// and the corrections path.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class ValidationBenchmarks
{
    private ActivityValidationService? _service;

    // Valid activity JSON payloads
    private string? _validCreateActivity;
    private string? _validFollowActivity;
    private string? _validAnnounceActivity;
    private string? _validUpdateActivity;
    private string? _validDeleteActivity;
    private string? _validLikeActivity;
    private string? _largeCreateActivity;

    // Invalid activity JSON payloads
    private string? _missingContext;
    private string? _missingType;
    private string? _missingActor;
    private string? _invalidIdUri;
    private string? _invalidTimestamp;
    private string? _malformedJson;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _service = new ActivityValidationService(NullLogger<ActivityValidationService>.Instance);

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        // Valid Create activity
        _validCreateActivity = JsonSerializer.Serialize(new Activity
        {
            Context = "https://www.w3.org/ns/activitystreams",
            Id = "https://example.com/users/alice/activities/create-001",
            Type = "Create",
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/notes/12345",
            Published = DateTime.UtcNow,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" },
            Cc = new List<string> { "https://example.com/users/alice/followers" },
        }, options);

        // Valid Follow activity
        _validFollowActivity = JsonSerializer.Serialize(new Activity
        {
            Context = "https://www.w3.org/ns/activitystreams",
            Id = "https://example.com/users/bob/activities/follow-001",
            Type = "Follow",
            Actor = "https://example.com/users/bob",
            Object = "https://example.com/users/alice",
            Published = DateTime.UtcNow,
        }, options);

        // Valid Announce activity
        _validAnnounceActivity = JsonSerializer.Serialize(new Activity
        {
            Context = "https://www.w3.org/ns/activitystreams",
            Id = "https://example.com/users/bob/activities/announce-001",
            Type = "Announce",
            Actor = "https://example.com/users/bob",
            Object = "https://example.com/users/alice/notes/12345",
            Published = DateTime.UtcNow,
        }, options);

        // Valid Update activity
        _validUpdateActivity = JsonSerializer.Serialize(new Activity
        {
            Context = "https://www.w3.org/ns/activitystreams",
            Id = "https://example.com/users/alice/activities/update-001",
            Type = "Update",
            Actor = "https://example.com/users/alice",
            Object = new
            {
                Context = "https://www.w3.org/ns/activitystreams",
                Id = "https://example.com/users/alice",
                Type = "Person",
                Name = "Alice Updated",
            },
            Published = DateTime.UtcNow,
        }, options);

        // Valid Delete activity
        _validDeleteActivity = JsonSerializer.Serialize(new Activity
        {
            Context = "https://www.w3.org/ns/activitystreams",
            Id = "https://example.com/users/alice/activities/delete-001",
            Type = "Delete",
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/notes/deleted-123",
            Published = DateTime.UtcNow,
        }, options);

        // Valid Like activity
        _validLikeActivity = JsonSerializer.Serialize(new Activity
        {
            Context = "https://www.w3.org/ns/activitystreams",
            Id = "https://example.com/users/bob/activities/like-001",
            Type = "Like",
            Actor = "https://example.com/users/bob",
            Object = "https://example.com/users/alice/notes/12345",
            Published = DateTime.UtcNow,
        }, options);

        // Large Create activity with embedded object
        _largeCreateActivity = JsonSerializer.Serialize(new Activity
        {
            Context = "https://www.w3.org/ns/activitystreams",
            Id = "https://example.com/users/alice/activities/create-large-001",
            Type = "Create",
            Actor = new Actor
            {
                Context = "https://www.w3.org/ns/activitystreams",
                Id = "https://example.com/users/alice",
                Type = "Person",
                Name = "Alice Example",
                PreferredUsername = "alice",
                Url = "https://example.com/users/alice",
                Inbox = "https://example.com/users/alice/inbox",
                Outbox = "https://example.com/users/alice/outbox",
                Followers = "https://example.com/users/alice/followers",
                Following = "https://example.com/users/alice/following",
                PublicKey = new PublicKey
                {
                    Id = "https://example.com/users/alice#main-key",
                    Owner = "https://example.com/users/alice",
                    PublicKeyPem = "-----BEGIN RSA PUBLIC KEY-----\nMIIBCgKCAQEA2a2rwplBQLHgCL3M3i8pM3UcH8MiU9D5jcb4OCFe0pE\n-----END RSA PUBLIC KEY-----",
                },
            },
            Object = new Note
            {
                Context = "https://www.w3.org/ns/activitystreams",
                Id = "https://example.com/notes/big-001",
                Type = "Note",
                Content = new string('X', 5000), // Large content payload
                AttributedTo = "https://example.com/users/alice",
                Published = DateTime.UtcNow,
                To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" },
                Tag = new List<string> { "#test", "#benchmark", "#validation" },
            },
            Published = DateTime.UtcNow,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" },
            Cc = new List<string> { "https://example.com/users/alice/followers" },
        }, options);

        // === Invalid activity payloads ===

        // Missing @context
        _missingContext = "{\"id\":\"https://example.com/activities/no-context\",\"type\":\"Create\",\"actor\":\"https://example.com/users/alice\",\"object\":\"https://example.com/notes/1\"}";

        // Missing type
        _missingType = "{\"@context\":\"https://www.w3.org/ns/activitystreams\",\"id\":\"https://example.com/activities/no-type\",\"actor\":\"https://example.com/users/alice\",\"object\":\"https://example.com/notes/1\"}";

        // Missing actor
        _missingActor = "{\"@context\":\"https://www.w3.org/ns/activitystreams\",\"id\":\"https://example.com/activities/no-actor\",\"type\":\"Create\",\"object\":\"https://example.com/notes/1\"}";

        // Invalid ID URI
        _invalidIdUri = "{\"@context\":\"https://www.w3.org/ns/activitystreams\",\"id\":\"not-a-valid-uri\",\"type\":\"Create\",\"actor\":\"https://example.com/users/alice\",\"object\":\"https://example.com/notes/1\"}";

        // Invalid timestamp
        _invalidTimestamp = "{\"@context\":\"https://www.w3.org/ns/activitystreams\",\"id\":\"https://example.com/activities/bad-timestamp\",\"type\":\"Create\",\"actor\":\"https://example.com/users/alice\",\"object\":\"https://example.com/notes/1\",\"published\":\"not-a-date\"}";

        // Malformed JSON
        _malformedJson = "{\"context\": \"https://www.w3.org/ns/activitystreams\", \"type\": \"Create\", broken json here";
    }

    // ===== Valid Activity Validation Benchmarks =====

    [Benchmark(Baseline = true)]
    public bool ValidateCreateActivity()
        => _service!.Validate(_validCreateActivity!, out _);

    [Benchmark]
    public bool ValidateFollowActivity()
        => _service!.Validate(_validFollowActivity!, out _);

    [Benchmark]
    public bool ValidateAnnounceActivity()
        => _service!.Validate(_validAnnounceActivity!, out _);

    [Benchmark]
    public bool ValidateUpdateActivity()
        => _service!.Validate(_validUpdateActivity!, out _);

    [Benchmark]
    public bool ValidateDeleteActivity()
        => _service!.Validate(_validDeleteActivity!, out _);

    [Benchmark]
    public bool ValidateLikeActivity()
        => _service!.Validate(_validLikeActivity!, out _);

    [Benchmark]
    public bool ValidateLargeActivityWithEmbeddedObject()
        => _service!.Validate(_largeCreateActivity!, out _);

    // ===== Invalid Activity Validation Benchmarks =====

    [Benchmark]
    public bool ValidateMissingContext()
        => _service!.Validate(_missingContext!, out _);

    [Benchmark]
    public bool ValidateMissingType()
        => _service!.Validate(_missingType!, out _);

    [Benchmark]
    public bool ValidateMissingActor()
        => _service!.Validate(_missingActor!, out _);

    [Benchmark]
    public bool ValidateInvalidIdUri()
        => _service!.Validate(_invalidIdUri!, out _);

    [Benchmark]
    public bool ValidateInvalidTimestamp()
        => _service!.Validate(_invalidTimestamp!, out _);

    [Benchmark]
    public bool ValidateMalformedJson()
        => _service!.Validate(_malformedJson!, out _);

    // ===== Correction Path Benchmarks =====

    [Benchmark]
    public bool ValidateWithCorrections_Valid()
        => _service!.ValidateWithCorrections(_validCreateActivity!, out _, out _);

    [Benchmark]
    public bool ValidateWithCorrections_MissingFields()
        => _service!.ValidateWithCorrections(_missingContext!, out _, out _);

    [Benchmark]
    public bool ValidateWithCorrections_MalformedJson()
        => _service!.ValidateWithCorrections(_malformedJson!, out _, out _);
}
