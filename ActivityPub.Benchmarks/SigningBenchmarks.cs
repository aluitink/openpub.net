using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActivityPub.Benchmarks;

/// <summary>
/// Benchmarks cryptographic key generation and HTTP signature operations
/// used by the ActivityPub federation protocol.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class SigningBenchmarks
{
    private KeyGenerationService? _keyService;
    private OutboundSigningService? _signingService;
    private string? _privateKeyPem;
    private string? _publicKeyPem;
    private RSA? _rsaKey;
    private HttpRequestMessage? _requestWithBody;
    private HttpRequestMessage? _requestWithoutBody;
    private string? _keyId;
    private string? _hostname;
    private Actor? _actorPayload;
    private string? _actorJson;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _keyService = new KeyGenerationService();
        _signingService = new OutboundSigningService(NullLogger<OutboundSigningService>.Instance);

        // Generate a key pair once for all benchmarks that need it
        var (privateKey, publicKey) = _keyService.GenerateRSAKeyPair();
        _privateKeyPem = privateKey;
        _publicKeyPem = publicKey;
        _keyId = "https://example.com/users/alice#main-key";
        _hostname = "remote.example.com";

        // Create RSA key for direct signing benchmarks
        _rsaKey = RSA.Create(2048);
        var exportedPrivatePem = ExportPrivateKeyToPem(_rsaKey);
        _privateKeyPem = exportedPrivatePem;

        // Prepare request payloads
        _actorPayload = new Actor
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
                Id = _keyId,
                Owner = _actorPayload!.Id,
                PublicKeyPem = _publicKeyPem,
            },
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        _actorJson = JsonSerializer.Serialize(_actorPayload, options);

        _requestWithBody = new HttpRequestMessage(HttpMethod.Post, "https://remote.example.com/inbox")
        {
            RequestUri = new Uri("https://remote.example.com/inbox"),
            Content = new StringContent(_actorJson, Encoding.UTF8, "application/activity+json"),
            Headers = { Date = DateTimeOffset.UtcNow },
        };

        _requestWithoutBody = new HttpRequestMessage(HttpMethod.Get, "https://remote.example.com/users/alice")
        {
            RequestUri = new Uri("https://remote.example.com/users/alice"),
            Headers = { Date = DateTimeOffset.UtcNow },
        };
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _rsaKey?.Dispose();
        _requestWithBody?.Dispose();
        _requestWithoutBody?.Dispose();
    }

    // ===== RSA Key Generation Benchmarks =====

    [Benchmark(Baseline = true)]
    public void GenerateRSA2048KeyPair()
    {
        _keyService!.GenerateRSAKeyPair();
    }

    [Benchmark]
    public void CreateRSA2048Key()
    {
        using var rsa = RSA.Create(2048);
    }

    // ===== PEM Export Benchmarks =====

    [Benchmark]
    public string ExportPrivateKeyToPEM()
    {
        using var rsa = RSA.Create(2048);
        return ExportPrivateKeyToPem(rsa);
    }

    [Benchmark]
    public string ExportPublicKeyToPEM()
    {
        using var rsa = RSA.Create(2048);
        return ExportPublicKeyToPem(rsa);
    }

    // ===== PEM Import Benchmarks =====

    [Benchmark]
    public RSA ImportPrivateKeyFromPEM()
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(_privateKeyPem!.AsSpan());
        return rsa;
    }

    [Benchmark]
    public RSA ImportPublicKeyFromPEM()
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(_publicKeyPem!.AsSpan());
        return rsa;
    }

    // ===== RSA Signing Benchmarks (raw) =====

    [Benchmark]
    public byte[] RSASignSHA256()
    {
        var data = Encoding.UTF8.GetBytes("benchmark signature data");
        using var rsa = RSA.Create(2048);
        return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    // ===== Full Outbound Signing Service Benchmarks =====

    [Benchmark]
    public void SignRequestWithBody()
    {
        var request = CloneRequest(_requestWithBody!);
        _signingService!.SignRequest(request, _privateKeyPem!, _keyId!, _hostname!);
        request.Dispose();
    }

    [Benchmark]
    public void SignRequestWithoutBody()
    {
        var request = CloneRequest(_requestWithoutBody!);
        _signingService!.SignRequest(request, _privateKeyPem!, _keyId!, _hostname!);
        request.Dispose();
    }

    // ===== Digest Computation Benchmarks =====

    [Benchmark]
    public string ComputeSHA256Digest()
    {
        var bodyBytes = Encoding.UTF8.GetBytes(_actorJson!);
        var hash = SHA256.HashData(bodyBytes);
        var digest = Convert.ToBase64String(hash);
        return $"SHA-256={digest}";
    }

    // ===== Helper Methods =====

    private static string ExportPrivateKeyToPem(RSA rsa)
    {
        byte[] privateKeyBytes = rsa.ExportRSAPrivateKey();
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN RSA PRIVATE KEY-----");
        string base64 = Convert.ToBase64String(privateKeyBytes);
        for (int i = 0; i < base64.Length; i += 64)
        {
            sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
        }
        sb.AppendLine("-----END RSA PRIVATE KEY-----");
        return sb.ToString();
    }

    private static string ExportPublicKeyToPem(RSA rsa)
    {
        byte[] publicKeyBytes = rsa.ExportRSAPublicKey();
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN PUBLIC KEY-----");
        string base64 = Convert.ToBase64String(publicKeyBytes);
        for (int i = 0; i < base64.Length; i += 64)
        {
            sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
        }
        sb.AppendLine("-----END PUBLIC KEY-----");
        return sb.ToString();
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri!)
        {
            Version = original.Version,
        };

        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (original.Content != null)
        {
            // Clone by serializing the content body
            var bodyTask = original.Content.ReadAsStringAsync();
            bodyTask.Wait();
            clone.Content = new StringContent(bodyTask.Result, Encoding.UTF8, original.Content.Headers.ContentType?.MediaType);
        }

        return clone;
    }
}
