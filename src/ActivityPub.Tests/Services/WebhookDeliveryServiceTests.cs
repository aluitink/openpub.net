using System.Net;
using System.Security.Cryptography;
using System.Text;
using ActivityPub.Core.Implementations;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Services;
using Moq;
using Xunit;

namespace ActivityPub.Tests.Services;

/// <summary>
/// Unit tests for <see cref="WebhookDeliveryService"/> — the webhook delivery
/// path, which previously had no unit test (only a host-based DI check in
/// WebUI/ApiWebhookTests). Covers config upsert, event-type-matched queueing,
/// the delivery loop (success / failure / retry / terminal MaxRetriesExceeded /
/// missing-config), the HMAC signature header, and signature verification.
/// Uses the real InMemoryActivityPubRepository (implements all webhook methods
/// with real behavior) + a stub HttpMessageHandler for the HTTP leg.
///
/// Note: the InMemory repo mutates the SAME entity instance it stores (the
/// QueueWebhookDeliveryAsync / UpdateWebhookDeliveryAsync paths reuse the
/// reference), so the local reference stays in sync with the stored state —
/// the tests assert on that reference directly.
/// </summary>
public class WebhookDeliveryServiceTests
{
    private const string Actor = "https://me.example/users/alice";
    private const string Endpoint = "https://hooks.example/endpoint";

    private record Harness(WebhookDeliveryService Service, InMemoryActivityPubRepository Repo, StubHandler Handler);

    private static Harness Create(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var repo = new InMemoryActivityPubRepository();
        var handler = new StubHandler(responder);
        var service = new WebhookDeliveryService(repo, new HttpClient(handler));
        return new Harness(service, repo, handler);
    }

    private static WebhookConfigEntity Configure(
        InMemoryActivityPubRepository repo,
        string eventType = "All",
        string endpoint = Endpoint,
        string? secret = null,
        int maxRetries = 3)
    {
        var config = new WebhookConfigEntity
        {
            ActorId = Actor,
            EventType = eventType,
            EndpointUrl = endpoint,
            HttpMethod = "POST",
            Enabled = true,
            SecretKey = secret,
            MaxRetries = maxRetries,
            RetryDelaySeconds = 60,
            UseExponentialBackoff = true,
            DeliveryMethod = WebhookDeliveryMethod.HttpPost
        };
        // The InMemory repo methods are synchronous Task.FromResult, so a direct
        // .Result is safe (no thread-pool hop, no deadlock risk).
        Assert.True(repo.SaveWebhookConfigAsync(config).Result);
        return config;
    }

    private static Activity NoteActivity(string type = "Create") =>
        new() { Id = "https://me.example/notes/1", Type = type, Actor = Actor };

    // --- ConfigureWebhookAsync (upsert) ----------------------------------

    [Fact]
    public async Task ConfigureWebhook_CreatesNewConfig_ThenUpdatesExisting()
    {
        var (service, repo, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));

        Assert.True(await service.ConfigureWebhookAsync(Actor, "All", "https://hooks.example/v1", "POST", true, "sek", maxRetries: 5));
        var cfg = Assert.Single(await repo.GetWebhookConfigsAsync(Actor));
        Assert.Equal("https://hooks.example/v1", cfg.EndpointUrl);
        Assert.Equal(5, cfg.MaxRetries);
        Assert.Equal("sek", cfg.SecretKey);

        // Re-configuring the same (actor, eventType) must update, not duplicate.
        Assert.True(await service.ConfigureWebhookAsync(Actor, "All", "https://hooks.example/v2", "POST", true, "sek", maxRetries: 5));
        var after = Assert.Single(await repo.GetWebhookConfigsAsync(Actor));
        Assert.Equal("https://hooks.example/v2", after.EndpointUrl);
    }

    [Fact]
    public async Task DeleteWebhookConfig_RemovesConfig()
    {
        var (service, repo, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var config = Configure(repo);

        Assert.True(await service.DeleteWebhookConfigAsync(config.Id));
        Assert.Empty(await repo.GetWebhookConfigsAsync(Actor));
    }

    // --- DeliverActivityToWebhooksAsync (event-type matching) ------------

    [Fact]
    public async Task DeliverActivity_TypeAll_MatchesAllActivityTypes()
    {
        var (service, repo, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));
        Configure(repo, eventType: "All");

        await service.DeliverActivityToWebhooksAsync(NoteActivity("Create"));
        await service.DeliverActivityToWebhooksAsync(NoteActivity("Like"));

        // "All" matches every type → two queued deliveries.
        Assert.Equal(2, (await repo.GetPendingWebhookDeliveriesAsync()).Count);
    }

    [Fact]
    public async Task DeliverActivity_TypeMismatch_QueueNothing()
    {
        var (service, repo, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));
        Configure(repo, eventType: "Like");

        // A "Create" activity must not match a "Like"-only webhook.
        await service.DeliverActivityToWebhooksAsync(NoteActivity("Create"));

        Assert.Empty(await repo.GetPendingWebhookDeliveriesAsync());
    }

    [Fact]
    public async Task DeliverActivity_DisabledConfig_QueueNothing()
    {
        var (service, repo, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var config = Configure(repo, eventType: "All");
        config.Enabled = false;
        Assert.True(repo.SaveWebhookConfigAsync(config).Result);

        await service.DeliverActivityToWebhooksAsync(NoteActivity("Create"));

        Assert.Empty(await repo.GetPendingWebhookDeliveriesAsync());
    }

    // --- ProcessPendingDeliveriesAsync (success / failure / retry) -------

    [Fact]
    public async Task ProcessPending_Success_MarksDelivered_RecordsHistory_Signs()
    {
        // Moq repo (records the history row) + a stubbed HttpClient for the
        // HTTP leg; the config is seeded directly.
        var repo = new Mock<IActivityPubRepository>();
        var config = new WebhookConfigEntity
        {
            Id = 1,
            ActorId = Actor,
            EventType = "All",
            EndpointUrl = Endpoint,
            HttpMethod = "POST",
            Enabled = true,
            SecretKey = "s3cret",
            MaxRetries = 3,
            RetryDelaySeconds = 60,
            UseExponentialBackoff = true,
            DeliveryMethod = WebhookDeliveryMethod.HttpPost
        };
        var delivery = new WebhookDeliveryEntity
        {
            Id = "d-1",
            ConfigId = "1",
            ActivityId = "https://me.example/notes/1",
            ActivityJson = "{\"id\":\"https://me.example/notes/1\",\"type\":\"Create\"}",
            ActorId = Actor,
            Status = WebhookDeliveryStatus.Queued
        };
        repo.Setup(r => r.GetWebhookConfigByIdAsync(1)).ReturnsAsync(config);
        repo.Setup(r => r.GetPendingWebhookDeliveriesAsync(It.IsAny<int>())).ReturnsAsync(new[] { delivery });
        repo.Setup(r => r.UpdateWebhookDeliveryAsync(It.IsAny<WebhookDeliveryEntity>())).ReturnsAsync(true);
        var captured = new List<WebhookDeliveryHistoryEntity>();
        repo.Setup(r => r.SaveWebhookDeliveryHistoryAsync(It.IsAny<WebhookDeliveryHistoryEntity>()))
            .Callback<WebhookDeliveryHistoryEntity>(captured.Add)
            .ReturnsAsync(true);

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        var service = new WebhookDeliveryService(repo.Object, new HttpClient(handler));

        await service.ProcessPendingDeliveriesAsync();

        // The delivery is marked Delivered with a 2xx response code.
        Assert.Equal(WebhookDeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(200, delivery.HttpResponseCode);

        // Exactly one HTTP hit, to the configured endpoint, with the HMAC
        // signature header matching the (secret, body) pair.
        var request = Assert.Single(handler.Requests);
        Assert.Equal(Endpoint, request.RequestUri!.ToString());
        Assert.Equal("POST", request.Method.Method);
        var body = await request.Content!.ReadAsStringAsync();
        var signature = request.Headers.TryGetValues("X-Webhook-Signature", out var sigValues)
            ? sigValues.Single() : null;
        Assert.Equal(ComputeHmacSha256Base64("s3cret", body), signature);

        // A history row was recorded with a 2xx status.
        var history = Assert.Single(captured);
        Assert.Equal("d-1", history.DeliveryId);
        Assert.Equal(200, history.HttpResponseCode);
    }

    [Fact]
    public async Task ProcessPending_Non2xx_IncrementsRetry_MarksFailed()
    {
        var harness = Create(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        Configure(harness.Repo, eventType: "All", maxRetries: 3);
        await harness.Service.DeliverActivityToWebhooksAsync(NoteActivity("Create"));
        // Keep a reference to the single queued delivery (InMemory mutates it in
        // place, so this reference reflects the stored state).
        var delivery = (await harness.Repo.GetPendingWebhookDeliveriesAsync()).Single();

        await harness.Service.ProcessPendingDeliveriesAsync();

        // First failure: RetryCount 0 < 3 → Failed, RetryCount 1, still pending.
        Assert.Equal(WebhookDeliveryStatus.Failed, delivery.Status);
        Assert.Equal(1, delivery.RetryCount);
        Assert.Equal(500, delivery.HttpResponseCode);
        Assert.Single(await harness.Repo.GetPendingWebhookDeliveriesAsync());
    }

    [Fact]
    public async Task ProcessPending_AtMaxRetries_MarksMaxRetriesExceeded_NoLongerPending()
    {
        var harness = Create(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        Configure(harness.Repo, eventType: "All", maxRetries: 2);
        await harness.Service.DeliverActivityToWebhooksAsync(NoteActivity("Create"));
        var delivery = (await harness.Repo.GetPendingWebhookDeliveriesAsync()).Single();

        // Attempt 1: RetryCount 0 < 2 → Failed, RetryCount 1 (still pending).
        await harness.Service.ProcessPendingDeliveriesAsync();
        Assert.Single(await harness.Repo.GetPendingWebhookDeliveriesAsync());

        // Attempt 2: RetryCount 1 < 2 → Failed, RetryCount 2 (still pending).
        await harness.Service.ProcessPendingDeliveriesAsync();
        Assert.Single(await harness.Repo.GetPendingWebhookDeliveriesAsync());

        // Attempt 3: RetryCount 2 >= 2 → MaxRetriesExceeded, no longer pending.
        await harness.Service.ProcessPendingDeliveriesAsync();
        Assert.Empty(await harness.Repo.GetPendingWebhookDeliveriesAsync());
        Assert.Equal(WebhookDeliveryStatus.MaxRetriesExceeded, delivery.Status);
        Assert.Equal(2, delivery.RetryCount);
    }

    [Fact]
    public async Task ProcessPending_MissingConfig_MarksFailed_NotFound()
    {
        var harness = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));
        // Queue a delivery whose ConfigId points at a config that doesn't exist.
        var delivery = new WebhookDeliveryEntity
        {
            ConfigId = "999",
            ActivityId = "https://me.example/notes/1",
            ActivityJson = "{}",
            ActorId = Actor,
            Status = WebhookDeliveryStatus.Queued
        };
        Assert.True(harness.Repo.QueueWebhookDeliveryAsync(delivery).Result);

        await harness.Service.ProcessPendingDeliveriesAsync();

        Assert.Equal(WebhookDeliveryStatus.Failed, delivery.Status);
        Assert.Equal("Webhook configuration not found", delivery.FailureReason);
        // No HTTP call should have been made (config was null → continue).
        Assert.Empty(harness.Handler.Requests);
    }

    // --- VerifyWebhookSignatureAsync -------------------------------------

    [Fact]
    public async Task VerifyWebhookSignature_MatchingSecret_ReturnsTrue()
    {
        var (service, _, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));
        const string secret = "topsecret";
        const string payload = "{\"id\":\"1\"}";
        var signature = ComputeHmacSha256Base64(secret, payload);

        Assert.True(await service.VerifyWebhookSignatureAsync(secret, payload, signature));
        Assert.False(await service.VerifyWebhookSignatureAsync(secret, payload, "wrong-signature"));
        Assert.False(await service.VerifyWebhookSignatureAsync("other-secret", payload, signature));
        Assert.False(await service.VerifyWebhookSignatureAsync(secret, payload + "tampered", signature));
    }

    private static string ComputeHmacSha256Base64(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public readonly List<HttpRequestMessage> Requests = new();
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responder(request));
        }
    }
}
