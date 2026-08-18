using System.Net;
using ActivityPub.Core.BackgroundServices;
using ActivityPub.Core.Options;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ActivityPub.Tests.BackgroundServices;

/// <summary>
/// Unit tests for <see cref="PeerHealthBackgroundService"/> — the timer-driven
/// federation peer liveness-probe service, which previously had no direct unit
/// test. The per-probe work is private, so a small <see cref="TestablePeerHealthService"/>
/// exposes the protected <c>ExecuteAsync</c>. Tests drive the public surface with a
/// real DI provider (mock <see cref="IPeerHealthService"/> + a scripted
/// <see cref="IHttpClientFactory"/> returning a stub <see cref="HttpMessageHandler"/>)
/// and verify the recorded probe outcomes against the HTTP responses the stub
/// handler returns.
/// </summary>
public class PeerHealthBackgroundServiceTests
{
    /// <summary>
    /// Exposes the protected <see cref="BackgroundService.ExecuteAsync"/> so the
    /// service's run loop can be driven directly in a test.
    /// </summary>
    private sealed class TestablePeerHealthService : PeerHealthBackgroundService
    {
        public TestablePeerHealthService(
            IServiceProvider serviceProvider,
            IOptions<ActivityPubOptions> options,
            ILogger<PeerHealthBackgroundService> logger)
            : base(serviceProvider, options, logger)
        {
        }

        public Task Run(CancellationToken token) => ExecuteAsync(token);
    }

    /// <summary>
    /// An <see cref="HttpMessageHandler"/> that returns a scripted status code.
    /// </summary>
    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        public StubHttpHandler(HttpStatusCode status) => _status = status;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_status) { Content = new StringContent("ok") };
            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// An <see cref="IHttpClientFactory"/> that returns an <see cref="HttpClient"/>
    /// backed by the provided handler.
    /// </summary>
    private sealed class StubHttpFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubHttpFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private static IOptions<ActivityPubOptions> BuildOptions(PeerHealthOptions options)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ActivityPubOptions:PeerHealth:Enabled"] = options.Enabled.ToString(),
                ["ActivityPubOptions:PeerHealth:ProbeIntervalMinutes"] = options.ProbeIntervalMinutes.ToString()
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions();
        services.Configure<ActivityPubOptions>(configuration.GetSection("ActivityPubOptions"));
        return services.BuildServiceProvider().GetRequiredService<IOptions<ActivityPubOptions>>();
    }

    private static (TestablePeerHealthService service, Mock<IPeerHealthService> peerHealth) Build(
        PeerHealthOptions options,
        IHttpClientFactory httpFactory,
        IEnumerable<FederationPeerEntity> peers)
    {
        var peerHealth = new Mock<IPeerHealthService>();
        peerHealth.Setup(p => p.GetPeersAsync(It.IsAny<bool>()))
                  .ReturnsAsync(peers.ToList());
        peerHealth.Setup(p => p.RecordProbeOutcomeAsync(It.IsAny<string>(), It.IsAny<bool>()))
                  .ReturnsAsync(true);

        var services = new ServiceCollection();
        services.AddSingleton<IPeerHealthService>(peerHealth.Object);
        services.AddSingleton(httpFactory);
        var optionsInstance = BuildOptions(options);
        services.AddSingleton(optionsInstance);
        var provider = services.BuildServiceProvider();

        var service = new TestablePeerHealthService(
            provider,
            optionsInstance,
            NullLogger<PeerHealthBackgroundService>.Instance);

        return (service, peerHealth);
    }

    private static async Task WaitFor(Func<bool> condition, TimeSpan timeout, string what)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && sw.Elapsed < timeout)
            await Task.Delay(50);
        Assert.True(condition(), $"timed out waiting for {what}");
    }

    private static async Task RunAndStopAsync(TestablePeerHealthService service)
    {
        var runTask = service.Run(CancellationToken.None);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await runTask.WaitAsync(cts.Token); }
        catch (OperationCanceledException) { /* expected on stop */ }
        finally { await service.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task Probe_ReachablePeer_RecordsReachable()
    {
        var factory = new StubHttpFactory(new StubHttpHandler(HttpStatusCode.OK));
        var (service, peerHealth) = Build(
            new PeerHealthOptions { Enabled = true, ProbeIntervalMinutes = 5 },
            factory,
            new[] { new FederationPeerEntity { Domain = "good.example.com" } });

        var runTask = service.Run(CancellationToken.None);
        await WaitFor(() =>
        {
            peerHealth.Verify(p => p.RecordProbeOutcomeAsync("good.example.com", true), Times.AtLeastOnce());
            return true;
        }, TimeSpan.FromSeconds(10), "the reachable probe outcome to be recorded");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await runTask.WaitAsync(cts.Token); }
        catch (OperationCanceledException) { /* expected on stop */ }
        finally { await service.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task Probe_UnreachablePeer_RecordsUnreachable()
    {
        var factory = new StubHttpFactory(new StubHttpHandler(HttpStatusCode.InternalServerError));
        var (service, peerHealth) = Build(
            new PeerHealthOptions { Enabled = true, ProbeIntervalMinutes = 5 },
            factory,
            new[] { new FederationPeerEntity { Domain = "bad.example.com" } });

        var runTask = service.Run(CancellationToken.None);
        await WaitFor(() =>
        {
            peerHealth.Verify(p => p.RecordProbeOutcomeAsync("bad.example.com", false), Times.AtLeastOnce());
            return true;
        }, TimeSpan.FromSeconds(10), "the unreachable probe outcome to be recorded");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await runTask.WaitAsync(cts.Token); }
        catch (OperationCanceledException) { /* expected on stop */ }
        finally { await service.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task Probe_NoPeers_DoesNothing()
    {
        var factory = new StubHttpFactory(new StubHttpHandler(HttpStatusCode.OK));
        var (service, peerHealth) = Build(
            new PeerHealthOptions { Enabled = true, ProbeIntervalMinutes = 5 },
            factory,
            Array.Empty<FederationPeerEntity>());

        await RunAndStopAsync(service);

        // No peers -> GetPeersAsync called but no probe outcomes recorded.
        peerHealth.Verify(p => p.GetPeersAsync(It.IsAny<bool>()), Times.AtLeastOnce());
        peerHealth.Verify(p => p.RecordProbeOutcomeAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task Probe_DisabledOption_DoesNotProbe()
    {
        var factory = new StubHttpFactory(new StubHttpHandler(HttpStatusCode.OK));
        var (service, peerHealth) = Build(
            new PeerHealthOptions { Enabled = false, ProbeIntervalMinutes = 5 },
            factory,
            new[] { new FederationPeerEntity { Domain = "off.example.com" } });

        await RunAndStopAsync(service);

        // Disabled -> the cycle returns before probing; no outcomes recorded.
        peerHealth.Verify(p => p.RecordProbeOutcomeAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }
}
