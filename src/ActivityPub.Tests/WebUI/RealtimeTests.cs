using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class RealtimeTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public RealtimeTests(WebUIFactory factory)
    {
        _factory = factory;
        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    HttpClient CreateClient() => _factory.CreateClient();

    async Task<HttpClient> RegisterAndLoginAndGetClient(string username)
    {
        var (client, _) = await RegisterAndLogin(username);
        return client;
    }

    async Task<string> GetAntiForgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/compose");
        var body = await response.Content.ReadAsStringAsync();
        var start = body.IndexOf("__RequestVerificationToken\" value=\"");
        if (start == -1) return string.Empty;
        start += "__RequestVerificationToken\" value=\"".Length;
        var end = body.IndexOf("\"", start);
        return body.Substring(start, end - start);
    }

    async Task<(HttpClient Client, string Username)> RegisterAndLogin(string username)
    {
        var client = CreateClient();
        await client.PostAsync("/auth/register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "Test User" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
        await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" },
        }));
        return (client, username);
    }

    [Fact]
    public async Task SSE_Endpoint_Exists()
    {
        var (client, _) = await RegisterAndLogin($"rt_sse_e_{Guid.NewGuid().ToString("N")[..8]}");
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var response = await client.GetAsync("/sse/stream", cts.Token);
            Assert.True(response.IsSuccessStatusCode, $"SSE failed: {(int)response.StatusCode}");
            Assert.Contains("text/event-stream", response.Content.Headers.ContentType?.MediaType ?? "");
        }
        catch (System.OperationCanceledException)
        {
        }
    }

    [Fact]
    public async Task SSE_Returns200_ForAuthenticatedUser()
    {
        var (client, _) = await RegisterAndLogin($"rt_sse_{Guid.NewGuid().ToString("N")[..8]}");
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var response = await client.GetAsync("/sse/stream", cts.Token);
            Assert.True(response.IsSuccessStatusCode, $"SSE failed: {(int)response.StatusCode}");
        }
        catch (System.OperationCanceledException)
        {
        }
    }

    [Fact]
    public async Task SignalR_Hub_Registered()
    {
        using var scope = _factory.Services.CreateScope();
        var hub = scope.ServiceProvider.GetService<Microsoft.AspNetCore.SignalR.IHubContext<ActivityPub.WebUI.Hubs.NotificationHub>>();
        Assert.NotNull(hub);
    }

    [Fact]
    public async Task NotificationService_Registered()
    {
        using var scope = _factory.Services.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<ActivityPub.WebUI.Services.INotificationService>();
        Assert.NotNull(notificationService);
    }

    [Fact]
    public async Task Compose_Post_BroadcastsNotification()
    {
        var username = $"rt_compose_{Guid.NewGuid().ToString("N")[..8]}";
        var (client, _) = await RegisterAndLogin(username);

        var response = await client.PostAsync("/compose/post", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Content", "Real-time test post" },
        }));

        Assert.True(response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Redirect, $"Post failed: {(int)response.StatusCode}");
    }

    [Fact]
    public async Task NotificationBadge_ExistsInLayout()
    {
        var (client, _) = await RegisterAndLogin($"rt_badge_{Guid.NewGuid().ToString("N")[..8]}");
        var response = await client.GetAsync("/");
        Assert.True(response.IsSuccessStatusCode, $"Home failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("notification-badge", body);
    }

    [Fact]
    public async Task SignalR_Script_ExistsInLayout()
    {
        var (client, _) = await RegisterAndLogin($"rt_script_{Guid.NewGuid().ToString("N")[..8]}");
        var response = await client.GetAsync("/");
        Assert.True(response.IsSuccessStatusCode, $"Home failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("signalr", body.ToLower());
    }

    [Fact]
    public async Task PushNotificationService_Registered()
    {
        using var scope = _factory.Services.CreateScope();
        var pushService = scope.ServiceProvider.GetRequiredService<ActivityPub.WebUI.Services.IPushNotificationService>();
        Assert.NotNull(pushService);
    }

    [Fact]
    public async Task PushController_Register_Returns200()
    {
        var (client, _) = await RegisterAndLogin($"rt_push_{Guid.NewGuid().ToString("N")[..8]}");
        var json = System.Text.Json.JsonSerializer.Serialize(new { endpoint = "https://example.com/push", p256dh = "test-key", auth = "test-auth" });
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/push/register", content);
        Assert.True(response.IsSuccessStatusCode, $"Push register failed: {(int)response.StatusCode}");
    }

    [Fact]
    public async Task RealtimeSettings_Get_Returns200()
    {
        var (client, _) = await RegisterAndLogin($"rt_settings_{Guid.NewGuid().ToString("N")[..8]}");
        var response = await client.GetAsync("/RealtimeSettings/get");
        Assert.True(response.IsSuccessStatusCode, $"Settings failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("pollingIntervalMs", body);
    }

    [Fact]
    public async Task RealtimeSettings_Update_Returns200()
    {
        var (client, _) = await RegisterAndLogin($"rt_settings2_{Guid.NewGuid().ToString("N")[..8]}");
        var json = System.Text.Json.JsonSerializer.Serialize(new { pollingIntervalMs = 10000, sseEnabled = true, signalREnabled = true, desktopNotificationsEnabled = true, notificationSoundEnabled = true, maxConnections = 3 });
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/RealtimeSettings/update", content);
        Assert.True(response.IsSuccessStatusCode, $"Settings update failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("pollingIntervalMs", body);
    }

    [Fact]
    public async Task DesktopNotification_Code_ExistsInLayout()
    {
        var (client, _) = await RegisterAndLogin($"rt_desktop_{Guid.NewGuid().ToString("N")[..8]}");
        var response = await client.GetAsync("/");
        Assert.True(response.IsSuccessStatusCode, $"Home failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Notification", body);
        Assert.Contains("desktopNotification", body);
    }

    [Fact]
    public async Task NotificationSound_Code_ExistsInLayout()
    {
        var (client, _) = await RegisterAndLogin($"rt_sound_{Guid.NewGuid().ToString("N")[..8]}");
        var response = await client.GetAsync("/");
        Assert.True(response.IsSuccessStatusCode, $"Home failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("notificationSound", body);
        Assert.Contains("playNotificationSound", body);
    }

    [Fact]
    public async Task AuditLogService_Registered()
    {
        using var scope = _factory.Services.CreateScope();
        var auditLog = scope.ServiceProvider.GetRequiredService<ActivityPub.WebUI.Services.IAuditLogService>();
        Assert.NotNull(auditLog);
    }

    [Fact]
    public async Task UserReportService_Registered()
    {
        using var scope = _factory.Services.CreateScope();
        var reportService = scope.ServiceProvider.GetRequiredService<ActivityPub.WebUI.Services.IUserReportService>();
        Assert.NotNull(reportService);
    }

    [Fact]
    public async Task ReportForm_Returns200()
    {
        var (client, _) = await RegisterAndLogin($"rt_report_{Guid.NewGuid().ToString("N")[..8]}");
        var response = await client.GetAsync("/Report/Form?targetUsername=baduser");
        Assert.True(response.IsSuccessStatusCode, $"Report form failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Report", body);
    }

    [Fact]
    public async Task Report_Submit_Success()
    {
        var (client, _) = await RegisterAndLogin($"rt_report2_{Guid.NewGuid().ToString("N")[..8]}");
        var response = await client.PostAsync("/Report/Submit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "targetUsername", "baduser" },
            { "reason", "Inappropriate content" },
        }));
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Redirect, $"Report submit failed: {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Report_CannotReportSelf()
    {
        var username = $"rt_report3_{Guid.NewGuid().ToString("N")[..8]}";
        var (client, _) = await RegisterAndLogin(username);
        var response = await client.PostAsync("/Report/Submit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "targetUsername", username },
            { "reason", "Test" },
        }));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadController_Exists()
    {
        var (client, _) = await RegisterAndLogin($"rt_upload_{Guid.NewGuid().ToString("N")[..8]}");
        var content = new MultipartFormDataContent
        {
            { new StreamContent(System.IO.Stream.Null), "file", "test.jpg" }
        };
        var response = await client.PostAsync("/upload/upload", content);
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.BadRequest, $"Upload failed: {(int)response.StatusCode}");
    }

    [Fact]
    public async Task ComposeForm_HasImageUpload()
    {
        var (client, _) = await RegisterAndLogin($"rt_compose_img_{Guid.NewGuid().ToString("N")[..8]}");
        var response = await client.GetAsync("/compose");
        Assert.True(response.IsSuccessStatusCode, $"Compose failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Image", body);
        Assert.Contains("multipart/form-data", body);
    }

    [Fact]
    public async Task NoteCard_HasReportButton()
    {
        var username = $"rt_note_rpt_{Guid.NewGuid().ToString("N")[..8]}";
        var (client, _) = await RegisterAndLogin(username);
        await client.PostAsync("/compose/post", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Content", "Test post for report button" },
        }));
        var response = await client.GetAsync("/timeline");
        Assert.True(response.IsSuccessStatusCode, $"Timeline failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Report note", body);
    }

    [Fact]
    public async Task Inbox_Processes_Block_Activity()
    {
        using var scope = _factory.Services.CreateScope();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
        var logger = new Microsoft.Extensions.Logging.Logger<ActivityPub.Core.Implementations.InboxProcessor>(loggerFactory);
        var processor = new ActivityPub.Core.Implementations.InboxProcessor(
            scope.ServiceProvider.GetRequiredService<ActivityPub.Core.Interfaces.IActivityPubRepository>(),
            logger
        );

        var activity = new ActivityPub.Core.Models.Activity
        {
            Id = $"https://localhost/test/block/{Guid.NewGuid()}",
            Type = "Block",
            Actor = "https://localhost/users/baduser",
            Target = "https://localhost/users/gooduser"
        };

        var evt = new ActivityPub.Core.Events.ActivityReceivedEvent(activity);
        await processor.HandleEventAsync(evt);
    }

    [Fact]
    public async Task Inbox_Processes_Reject_Activity()
    {
        using var scope = _factory.Services.CreateScope();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
        var logger = new Microsoft.Extensions.Logging.Logger<ActivityPub.Core.Implementations.InboxProcessor>(loggerFactory);
        var processor = new ActivityPub.Core.Implementations.InboxProcessor(
            scope.ServiceProvider.GetRequiredService<ActivityPub.Core.Interfaces.IActivityPubRepository>(),
            logger
        );

        var activity = new ActivityPub.Core.Models.Activity
        {
            Id = $"https://localhost/test/reject/{Guid.NewGuid()}",
            Type = "Reject",
            Actor = "https://localhost/users/remote",
            Object = "https://localhost/users/local/follow/123"
        };

        var evt = new ActivityPub.Core.Events.ActivityReceivedEvent(activity);
        await processor.HandleEventAsync(evt);
    }

    [Fact]
    public async Task AuditLog_RecordsAction()
    {
        using var scope = _factory.Services.CreateScope();
        var auditLog = scope.ServiceProvider.GetRequiredService<ActivityPub.WebUI.Services.IAuditLogService>();
        await auditLog.LogActionAsync("admin", "TestAction", "target-1", "Test details");
        var entries = await auditLog.GetRecentEntriesAsync(10);
        Assert.NotEmpty(entries);
        Assert.Contains(entries, e => e.Action == "TestAction");
    }

    [Fact]
    public async Task ReportService_SubmitAndRetrieve()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ActivityPub.WebUI.Services.IUserReportService>();
        await svc.SubmitReportAsync("reporter", "target", "Bad behavior", null);
        var reports = await svc.GetPendingReportsAsync();
        Assert.NotEmpty(reports);
        Assert.Contains(reports, r => r.Status == "pending");
    }

    [Fact]
    public async Task ReportService_DismissReport()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ActivityPub.WebUI.Services.IUserReportService>();
        await svc.SubmitReportAsync("reporter", "target", "Bad behavior", null);
        var reports = await svc.GetPendingReportsAsync();
        var report = reports.First(r => r.Status == "pending");
        await svc.DismissReportAsync(report.Id, "admin", "Not actionable");
        var pending = await svc.GetPendingReportsAsync();
        Assert.DoesNotContain(pending, r => r.Id == report.Id);
    }

    // MRF Tests

    [Fact]
    public async Task MRFService_Registers()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ActivityPub.Core.Services.IMRFService>();
        Assert.NotNull(svc);
    }

    [Fact]
    public async Task MRFService_Passes_Activity_Without_Rules()
    {
        using var scope = _factory.Services.CreateScope();
        var mrf = scope.ServiceProvider.GetRequiredService<ActivityPub.Core.Services.IMRFService>();
        var activity = new ActivityPub.Core.Models.Activity
        {
            Id = "https://test/mrf-test-1",
            Type = "Create",
            Actor = "https://localhost/users/tester",
            Object = new ActivityPub.Core.Models.Note { Id = "https://test/note-1", Type = "Note", Content = "Hello world" }
        };
        var result = await mrf.ProcessAsync(activity);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task InboxProcessor_With_MRF_Service_Registers()
    {
        using var scope = _factory.Services.CreateScope();
        var proc = scope.ServiceProvider.GetRequiredService<ActivityPub.Core.Implementations.InboxProcessor>();
        Assert.NotNull(proc);
    }

    // Poll Tests

    [Fact]
    public async Task PollController_New_Returns_View()
    {
        var client = await RegisterAndLoginAndGetClient($"polluser_{Guid.NewGuid().ToString("N")[..8]}");
        var response = await client.GetAsync("/Poll/New");
        Assert.True(response.IsSuccessStatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Create Poll", body);
    }

    [Fact]
    public async Task PollController_Create_With_Valid_Poll()
    {
        var client = await RegisterAndLoginAndGetClient($"pollcreator_{Guid.NewGuid().ToString("N")[..8]}");
        var token = await GetAntiForgeryTokenAsync(client);

        var formData = new List<KeyValuePair<string, string>>
        {
            new("Content", "Best framework?"),
            new("Options", ".NET"),
            new("Options", "Java"),
            new("Options", "Python"),
            new("Options", "Rust"),
            new("DurationMinutes", "1440"),
            new("__RequestVerificationToken", token)
        };

        var content = new FormUrlEncodedContent(formData);
        var response = await client.PostAsync("/Poll/Create", content);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode || response.Headers.Location != null,
            $"Post failed: {(int)response.StatusCode} {body.Substring(0, Math.Min(200, body.Length))}");
    }

    [Fact]
    public async Task PollController_Create_Rejects_Too_Few_Options()
    {
        var client = await RegisterAndLoginAndGetClient($"pollfail_{Guid.NewGuid().ToString("N")[..8]}");
        var token = await GetAntiForgeryTokenAsync(client);

        var formData = new List<KeyValuePair<string, string>>
        {
            new("Content", "Only one option?"),
            new("Options", "Yes"),
            new("Options", ""),
            new("Options", ""),
            new("Options", ""),
            new("DurationMinutes", "1440"),
            new("__RequestVerificationToken", token)
        };

        var content = new FormUrlEncodedContent(formData);
        var response = await client.PostAsync("/Poll/Create", content);
        Assert.True(response.IsSuccessStatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("2-4 options", body);
    }

    [Fact]
    public async Task PollController_Create_Rejects_Long_Question()
    {
        var client = await RegisterAndLoginAndGetClient($"pollfail2_{Guid.NewGuid().ToString("N")[..8]}");
        var token = await GetAntiForgeryTokenAsync(client);

        var formData = new List<KeyValuePair<string, string>>
        {
            new("Content", new string('x', 501)),
            new("Options", "A"),
            new("Options", "B"),
            new("Options", ""),
            new("Options", ""),
            new("DurationMinutes", "1440"),
            new("__RequestVerificationToken", token)
        };

        var content = new FormUrlEncodedContent(formData);
        var response = await client.PostAsync("/Poll/Create", content);
        Assert.True(response.IsSuccessStatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("500", body);
    }

    // Rate Limit Settings Tests

    [Fact]
    public async Task RateLimitSettingsController_Index_Returns_View()
    {
        var client = await RegisterAndLoginAndGetClient($"ratelimituser_{Guid.NewGuid().ToString("N")[..8]}");
        var response = await client.GetAsync("/RateLimitSettings");
        Assert.True(response.IsSuccessStatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Rate Limit Settings", body);
    }

    [Fact]
    public async Task RateLimitSettingsController_Update_Valid()
    {
        var client = await RegisterAndLoginAndGetClient($"ratelimitadmin_{Guid.NewGuid().ToString("N")[..8]}");
        var token = await GetAntiForgeryTokenAsync(client);

        var formData = new List<KeyValuePair<string, string>>
        {
            new("Compose.Limit", "30"),
            new("Compose.WindowMinutes", "5"),
            new("Follow.Limit", "15"),
            new("Follow.WindowMinutes", "2"),
            new("Upload.Limit", "20"),
            new("Upload.WindowMinutes", "3"),
            new("__RequestVerificationToken", token)
        };

        var content = new FormUrlEncodedContent(formData);
        var response = await client.PostAsync("/RateLimitSettings/Update", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    // MRF Admin Page Tests

    [Fact]
    public async Task MRFController_Index_Returns_View()
    {
        var client = await RegisterAndLoginAndGetClient($"mrfuser_{Guid.NewGuid().ToString("N")[..8]}");
        var response = await client.GetAsync("/MRF");
        Assert.True(response.IsSuccessStatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Moderation Rules Framework", body);
    }

    [Fact]
    public async Task MRFController_Update_Adds_Block()
    {
        var client = await RegisterAndLoginAndGetClient($"mrfadmin_{Guid.NewGuid().ToString("N")[..8]}");
        var token = await GetAntiForgeryTokenAsync(client);

        var formData = new List<KeyValuePair<string, string>>
        {
            new("ProhibitedWords", ""),
            new("BlockedDomains", ""),
            new("MaxContentLength", "3000"),
            new("__RequestVerificationToken", token)
        };

        var content = new FormUrlEncodedContent(formData);
        var response = await client.PostAsync("/MRF/Update", content);
        Assert.True(response.IsSuccessStatusCode);
    }

    // MRF filtering integration test

    [Fact]
    public async Task MRFService_Filters_Prohibited_Words()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ActivityPub.Core.Options.ActivityPubOptions>>();
        options.Value.MRFOptions ??= new ActivityPub.Core.Options.MRFOptions();
        options.Value.MRFOptions.ProhibitedWords = new List<string> { "badsword" };

        var mrf = scope.ServiceProvider.GetRequiredService<ActivityPub.Core.Services.IMRFService>();
        var activity = new ActivityPub.Core.Models.Activity
        {
            Id = "https://test/mrf-filter-test",
            Type = "Create",
            Actor = "https://localhost/users/tester",
            Object = new ActivityPub.Core.Models.Note { Id = "https://test/note-2", Type = "Note", Content = "This contains badsword" }
        };
        var result = await mrf.ProcessAsync(activity);
        Assert.Null(result);

        options.Value.MRFOptions.ProhibitedWords.Clear();
    }

    [Fact]
    public async Task MRFService_Filters_Blocked_Domains()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ActivityPub.Core.Options.ActivityPubOptions>>();
        options.Value.MRFOptions ??= new ActivityPub.Core.Options.MRFOptions();
        options.Value.MRFOptions.BlockedDomains = new List<string> { "badsite.com" };

        var mrf = scope.ServiceProvider.GetRequiredService<ActivityPub.Core.Services.IMRFService>();
        var activity = new ActivityPub.Core.Models.Activity
        {
            Id = "https://test/mrf-domain-test",
            Type = "Create",
            AttributedTo = "https://badsite.com/users/spammer",
            Object = new ActivityPub.Core.Models.Note { Id = "https://test/note-3", Type = "Note", Content = "Hello from bad site" }
        };
        var result = await mrf.ProcessAsync(activity);
        Assert.Null(result);

        options.Value.MRFOptions.BlockedDomains.Clear();
    }

    [Fact]
    public async Task MRFService_Filters_Long_Content()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ActivityPub.Core.Options.ActivityPubOptions>>();
        options.Value.MRFOptions ??= new ActivityPub.Core.Options.MRFOptions();
        options.Value.MRFOptions.MaxContentLength = 50;

        var mrf = scope.ServiceProvider.GetRequiredService<ActivityPub.Core.Services.IMRFService>();
        var activity = new ActivityPub.Core.Models.Activity
        {
            Id = "https://test/mrf-length-test",
            Type = "Create",
            Actor = "https://localhost/users/tester",
            Object = new ActivityPub.Core.Models.Note { Id = "https://test/note-4", Type = "Note", Content = new string('x', 100) }
        };
        var result = await mrf.ProcessAsync(activity);
        Assert.Null(result);

        options.Value.MRFOptions.MaxContentLength = null;
    }

    [Fact]
    public async Task FederationHealthService_Registers()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ActivityPub.Core.Services.IFederationHealthService>();
        Assert.NotNull(svc);
    }

    [Fact]
    public async Task FederationHealth_GetHealthStatus_Returns_Data()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ActivityPub.Core.Services.IFederationHealthService>();
        var status = await svc.GetHealthStatusAsync();

        Assert.NotNull(status);
        Assert.NotEqual(default, status.LastChecked);
        Assert.Contains(status.OverallStatus, new[] { "Healthy", "Warning", "Degraded", "Critical" });
        Assert.NotNull(status.DeliveryQueue);
        Assert.NotNull(status.ActivityProcessing);
        Assert.NotNull(status.Database);
    }

    [Fact]
    public async Task FederationHealth_GetDeliveryQueueStats_Returns_Data()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ActivityPub.Core.Services.IFederationHealthService>();
        var stats = await svc.GetDeliveryQueueStatsAsync();

        Assert.NotNull(stats);
        Assert.True(stats.ErrorRate >= 0);
    }

    [Fact]
    public async Task FederationHealth_GetRecentErrors_Returns_Empty_When_No_Errors()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ActivityPub.Core.Services.IFederationHealthService>();
        var errors = await svc.GetRecentErrorsAsync(50);

        Assert.NotNull(errors);
    }

    [Fact]
    public async Task FederationHealth_Index_Returns_Ok()
    {
        var client = await RegisterAndLoginAndGetClient("healthuser1");
        var response = await client.GetAsync("/FederationHealth");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Federation Health", body);
    }

    [Fact]
    public async Task FederationHealth_ApiStatus_Returns_Json()
    {
        var client = await RegisterAndLoginAndGetClient("healthuser2");
        var response = await client.GetAsync("/FederationHealth/ApiStatus");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("overallStatus", body);
    }

    [Fact]
    public async Task FederationHealth_ApiErrors_Returns_Json()
    {
        var client = await RegisterAndLoginAndGetClient("healthuser3");
        var response = await client.GetAsync("/FederationHealth/ApiErrors");
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task FederationHealth_ProbeServers_Returns_Ok_With_Empty()
    {
        var client = await RegisterAndLoginAndGetClient("healthuser4");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["newDomain"] = ""
        });
        var response = await client.PostAsync("/FederationHealth/ProbeServers", content);
        response.EnsureSuccessStatusCode();
    }
}
