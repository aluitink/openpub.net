using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using ActivityPub.Core.Models;
using ActivityPub.Core.Infrastructure;

namespace ActivityPub.Benchmarks;

/// <summary>
/// Benchmarks JSON serialization performance for core ActivityPub models.
/// Compares default System.Text.Json options vs. pre-configured shared options
/// vs. the custom WebFingerJsonConverter for WebFinger responses.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class JSONSerializationBenchmarks
{
    // Pre-configured shared JsonSerializerOptions to simulate production usage
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    // Optimized options that reuse encoders and disable unnecessary features
    private static readonly JsonSerializerOptions OptimizedOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new WebFingerJsonConverter() },
    };

    private Actor? _actor;
    private Activity? _activity;
    private Note? _note;
    private ActivityPub.Core.Models.Object? _pubObject;
    private WebFingerJrd? _webFingerJrd;
    private PublicKey? _publicKey;
    private byte[]? _actorUtf8Buffer;
    private byte[]? _activityUtf8Buffer;
    private byte[]? _noteUtf8Buffer;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _publicKey = new PublicKey
        {
            Id = "https://example.com/users/alice#main-key",
            Owner = "https://example.com/users/alice",
            PublicKeyPem = "-----BEGIN RSA PUBLIC KEY-----\nMIIBCgKCAQEA2a2rwplBQLHgCL3M3i8pM3UcH8MiU9D5jcb4OCFe0pE\n-----END RSA PUBLIC KEY-----"
        };

        _actor = new Actor
        {
            Context = "https://www.w3.org/ns/activitystreams",
            Id = "https://example.com/users/alice",
            Type = "Person",
            Name = "Alice Example",
            PreferredUsername = "alice",
            Url = "https://example.com/users/alice",
            PublicKey = _publicKey,
            Inbox = "https://example.com/users/alice/inbox",
            Outbox = "https://example.com/users/alice/outbox",
            Followers = "https://example.com/users/alice/followers",
            Following = "https://example.com/users/alice/following",
            Liked = "https://example.com/users/alice/liked",
            Icon = new Image { Url = "https://example.com/users/alice/avatar.jpg", MediaType = "image/jpeg" },
            Summary = "<p>A test actor for benchmarking purposes.</p>",
            Published = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Updated = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            Domain = "example.com",
            ManuallyApprovesFollowers = false,
            Endpoints = new Endpoints
            {
                SharedInbox = "https://example.com/inbox",
                ProxyUrl = "https://example.com/proxy",
            },
            SharedInbox = "https://example.com/inbox",
        };

        _note = new Note
        {
            Context = "https://www.w3.org/ns/activitystreams",
            Id = "https://example.com/users/alice/notes/12345",
            Type = "Note",
            Content = "This is a test note for benchmarking JSON serialization performance in ActivityPub.",
            AttributedTo = "https://example.com/users/alice",
            Published = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" },
            Tag = new List<string> { "#test", "#benchmark" },
        };

        _pubObject = new ActivityPub.Core.Models.Object
        {
            Context = "https://www.w3.org/ns/activitystreams",
            Id = "https://example.com/objects/67890",
            Type = "Article",
            Name = "Benchmarking ActivityPub Serialization",
            Content = "<p>Long-form article content for benchmarking purposes with HTML markup.</p>",
            MediaType = "text/html",
            Url = "https://example.com/articles/67890",
            AttributedTo = "https://example.com/users/alice",
            Published = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" },
        };

        _activity = new Activity
        {
            Context = "https://www.w3.org/ns/activitystreams",
            Id = "https://example.com/users/alice/activities/abc123",
            Type = "Create",
            Actor = _actor.Id,
            Object = _note,
            Published = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" },
            Cc = new List<string> { "https://example.com/users/alice/followers" },
        };

        _webFingerJrd = new WebFingerJrd
        {
            Subject = "acct:alice@example.com",
            Links = new List<WebFingerLink>
            {
                new() { Rel = "self", Type = "application/activity+json", Href = "https://example.com/users/alice" },
                new() { Rel = "http://oauth.net/core/1.0/request_token", Type = "application/x-www-form-urlencoded", Href = "https://example.com/oauth/request_token" },
                new() { Rel = "http://oauth.net/core/1.0/authorize", Type = "application/x-www-form-urlencoded", Href = "https://example.com/oauth/authorize" },
                new() { Rel = "http://oauth.net/core/1.0/access_token", Type = "application/x-www-form-urlencoded", Href = "https://example.com/oauth/access_token" },
                new() { Rel = "http://ostatus.org/schema/1.0/subscribe", Type = "application/x-www-form-urlencoded", Href = "https://example.com/users/alice/follow" },
            },
        };

        // Pre-serialize to get byte arrays for deserialization benchmarks
        _actorUtf8Buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_actor, DefaultOptions));
        _activityUtf8Buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_activity, DefaultOptions));
        _noteUtf8Buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_note, DefaultOptions));
    }

    // ===== Actor Serialization Benchmarks =====

    [Benchmark(Baseline = true)]
    public string SerializeActor_DefaultOptions()
        => JsonSerializer.Serialize(_actor!, DefaultOptions);

    [Benchmark]
    public string SerializeActor_OptimizedOptions()
        => JsonSerializer.Serialize(_actor, OptimizedOptions);

    [Benchmark]
    public byte[] SerializeActorToUtf8_DefaultOptions()
    {
        using var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, _actor!, typeof(Actor), DefaultOptions);
        return stream.ToArray();
    }

    [Benchmark]
    public byte[] SerializeActorToUtf8_OptimizedOptions()
    {
        using var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, _actor!, typeof(Actor), OptimizedOptions);
        return stream.ToArray();
    }

    // ===== Activity Serialization Benchmarks =====

    [Benchmark]
    public string SerializeActivity_DefaultOptions()
        => JsonSerializer.Serialize(_activity!, DefaultOptions);

    [Benchmark]
    public string SerializeActivity_OptimizedOptions()
        => JsonSerializer.Serialize(_activity, OptimizedOptions);

    [Benchmark]
    public byte[] SerializeActivityToUtf8_DefaultOptions()
    {
        using var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, _activity!, typeof(Activity), DefaultOptions);
        return stream.ToArray();
    }

    // ===== Note Serialization Benchmarks =====

    [Benchmark]
    public string SerializeNote_DefaultOptions()
        => JsonSerializer.Serialize(_note!, DefaultOptions);

    [Benchmark]
    public string SerializeNote_OptimizedOptions()
        => JsonSerializer.Serialize(_note, OptimizedOptions);

    // ===== Object Serialization Benchmarks =====

    [Benchmark]
    public string SerializeObject_DefaultOptions()
        => JsonSerializer.Serialize(_pubObject!, DefaultOptions);

    [Benchmark]
    public string SerializeObject_OptimizedOptions()
        => JsonSerializer.Serialize(_pubObject, OptimizedOptions);

    // ===== WebFinger Serialization Benchmarks =====

    [Benchmark]
    public string SerializeWebFinger_Default()
        => JsonSerializer.Serialize(_webFingerJrd!, DefaultOptions);

    [Benchmark]
    public string SerializeWebFinger_CustomConverter()
        => JsonSerializer.Serialize(_webFingerJrd!, OptimizedOptions);

    [Benchmark]
    public byte[] SerializeWebFinger_CustomConverter_Utf8()
    {
        using var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, _webFingerJrd!, typeof(WebFingerJrd), OptimizedOptions);
        return stream.ToArray();
    }

    // ===== PublicKey Serialization Benchmarks =====

    [Benchmark]
    public string SerializePublicKey_DefaultOptions()
        => JsonSerializer.Serialize(_publicKey!, DefaultOptions);

    [Benchmark]
    public string SerializePublicKey_OptimizedOptions()
        => JsonSerializer.Serialize(_publicKey, OptimizedOptions);

    // ===== Deserialization Benchmarks =====

    [Benchmark]
    public Actor? DeserializeActor_DefaultOptions()
        => JsonSerializer.Deserialize<Actor>(_actorUtf8Buffer!, DefaultOptions);

    [Benchmark]
    public Actor? DeserializeActor_OptimizedOptions()
        => JsonSerializer.Deserialize<Actor>(_actorUtf8Buffer!, OptimizedOptions);

    [Benchmark]
    public Activity? DeserializeActivity_DefaultOptions()
        => JsonSerializer.Deserialize<Activity>(_activityUtf8Buffer!, DefaultOptions);

    [Benchmark]
    public Activity? DeserializeActivity_OptimizedOptions()
        => JsonSerializer.Deserialize<Activity>(_activityUtf8Buffer!, OptimizedOptions);

    [Benchmark]
    public Note? DeserializeNote_DefaultOptions()
        => JsonSerializer.Deserialize<Note>(_noteUtf8Buffer!, DefaultOptions);

    [Benchmark]
    public Note? DeserializeNote_OptimizedOptions()
        => JsonSerializer.Deserialize<Note>(_noteUtf8Buffer!, OptimizedOptions);
}
