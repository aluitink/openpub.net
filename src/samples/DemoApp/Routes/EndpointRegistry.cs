using ActivityPub.Core;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DemoApp.Services;
using DemoApp.Services.OAuth2;
using System.Security.Cryptography;
using System.Text;

namespace DemoApp.Routing;

public static class EndpointRegistry
{
    public static void MapEndpoints(this WebApplication app)
    {
        app.MapGet("/", () => Results.Redirect("/index.html"));

        app.MapGet("/demo/keys", async (IKeyGenerationService keyService, IMemoryCache cache) =>
        {
            var cacheKey = "key_pair";
            var keys = cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return keyService.GenerateRSAKeyPair();
            });

            return Results.Ok(new
            {
                PrivateKey = keys.privateKeyPem,
                PublicKey = keys.publicKeyPem
            });
        })
        .WithTags("Cache");

        app.MapGet("/demo/actors", async (ActivityPubDbContext db, IMemoryCache cache) =>
        {
            var actors = await cache.GetOrCreateAsync("actors_list", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await db.Actors.AsNoTracking().OrderBy(a => a.Username).ToListAsync();
            });

            return Results.Ok(actors);
        })
        .WithTags("Actors")
        .CacheOutput(output => output.Expire(TimeSpan.FromMinutes(5)));

        app.MapPost("/demo/actors", async (ActivityPubDbContext db, HttpContext context, IMemoryCache cache) =>
        {
            var keyService = app.Services.GetRequiredService<IKeyGenerationService>();
            var keys = keyService.GenerateRSAKeyPair();
            string privateKey = keys.privateKeyPem;
            string publicKey = keys.publicKeyPem;

            var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            string username = requestBody.Trim('"');

            var actor = new ActorEntity
            {
                Username = username,
                JsonData = $"{{\"publicKey\":\"{publicKey}\"}}"
            };

            await db.Actors.AddAsync(actor);
            await db.SaveChangesAsync();

            cache.Remove("actors_list");
            cache.Remove($"actors_list_{username}");

            return Results.Created($"/actors/{actor.Id}", actor);
        })
        .WithTags("Actors");

        app.MapPost("/demo/activities", async (ActivityPubDbContext db, HttpContext context, IMemoryCache cache) =>
        {
            var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);
            string activityId = data?.GetValueOrDefault("activityId") ?? "";
            string jsonData = data?.GetValueOrDefault("jsonData") ?? "";

            var activity = new ActivityEntity
            {
                ActivityId = activityId,
                JsonData = jsonData
            };

            await db.Activities.AddAsync(activity);
            await db.SaveChangesAsync();

            cache.Remove("activities_list");

            var hubContext = app.Services.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<ActivityHub>>();
            await hubContext.Clients.All.SendAsync("ReceiveActivity", jsonData);

            return Results.Created($"/activities/{activity.Id}", activity);
        })
        .WithTags("Activities");

        app.MapGet("/demo/status", () =>
        {
            return Results.Ok(new
            {
                Service = "ActivityPub Demo",
                Version = "1.0.0",
                Status = "Running"
            });
        });

        app.MapGet("/demo/activities/paginated", async (ActivityPubDbContext db, int page = 1, int pageSize = 10, IMemoryCache cache = null) =>
        {
            var cacheKey = $"paginated_activities_{page}_{pageSize}";
            var activities = page <= 10 ? await cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await db.Activities.AsNoTracking()
                    .OrderByDescending(a => a.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();
            }) : await db.Activities.AsNoTracking()
                .OrderByDescending(a => a.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            int total = await db.Activities.CountAsync();

            return Results.Ok(new
            {
                Data = activities,
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                TotalPages = (int)Math.Ceiling((double)total / pageSize)
            });
        })
        .WithTags("Activities");

        app.MapGet("/demo/templates", () =>
        {
            string templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "templates");
            string templatesJsonPath = Path.Combine(templateDir, "templates.json");

            if (File.Exists(templatesJsonPath))
            {
                string json = File.ReadAllText(templatesJsonPath);
                return Results.Content(json, "application/json");
            }

            return Results.NotFound();
        });

        app.MapGet("/demo/templates/{templateId}", (string templateId) =>
        {
            string templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "templates");
            string templatePath = Path.Combine(templateDir, $"{templateId}.json");

            if (File.Exists(templatePath))
            {
                string json = File.ReadAllText(templatePath);
                return Results.Content(json, "application/json");
            }

            return Results.NotFound();
        });

        app.MapGet("/demo/config", (IConfiguration config) =>
        {
            ActivityPubConfig? activityPubSection = config.GetSection("ActivityPub").Get<ActivityPubConfig>();
            LoggingConfig? loggingSection = config.GetSection("Logging").Get<LoggingConfig>();

            return Results.Ok(new
            {
                ActivityPub = activityPubSection,
                Logging = loggingSection,
                Timestamp = DateTime.UtcNow
            });
        });

        app.MapPost("/demo/config", async (HttpContext context) =>
        {
            string requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(requestBody))
                return Results.BadRequest("Invalid configuration");

            string appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

            File.WriteAllText(appSettingsPath, requestBody);

            return Results.Ok(new
            {
                Success = true,
                Message = "Configuration updated",
                Timestamp = DateTime.UtcNow
            });
        });

        app.MapGet("/demo/config/validation", (IConfiguration config) =>
        {
            List<string> errors = new();

            string? domain = config["ActivityPub:Domain"];
            if (string.IsNullOrWhiteSpace(domain))
                errors.Add("Domain is required");

            return Results.Ok(new
            {
                Valid = !errors.Any(),
                Errors = errors
            });
        });

        app.MapGet("/demo/queues", () =>
        {
            return Results.Ok(new
            {
                Outbound = new
                {
                    Total = 0,
                    Pending = 0,
                    Processing = 0,
                    Completed = 0,
                    Failed = 0
                },
                Inbound = new
                {
                    Total = 0,
                    Pending = 0,
                    Processing = 0,
                    Completed = 0,
                    Failed = 0
                },
                Items = Array.Empty<QueueItem>()
            });
        });

        app.MapPost("/demo/queues/retry", () =>
        {
            return Results.Ok(new
            {
                Success = true,
                Retried = 0,
                Message = "No failed items to retry"
            });
        });

        app.MapPost("/demo/queues/clear", () =>
        {
            return Results.Ok(new
            {
                Success = true,
                Message = "Queue cleared"
            });
        });

        app.MapPost("/demo/http-signature/sign", async (HttpContext context, IConfiguration config) =>
        {
            string requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);

            string keyId = data?.GetValueOrDefault("keyId") ?? "";
            string privateKeyPem = data?.GetValueOrDefault("privateKey") ?? "";
            string urlString = data?.GetValueOrDefault("url") ?? "";
            string method = data?.GetValueOrDefault("method") ?? "POST";

            IKeyGenerationService keyService = app.Services.GetRequiredService<IKeyGenerationService>();

            return Results.Ok(new
            {
                Success = !string.IsNullOrWhiteSpace(keyId) && !string.IsNullOrWhiteSpace(privateKeyPem),
                KeyId = keyId,
                Algorithm = "rsa-sha256",
                Headers = new[] { "(request-target)", "host", "date", "digest", "content-length" },
                Timestamp = DateTime.UtcNow
            });
        });

        app.MapPost("/demo/http-signature/verify", async (HttpContext context) =>
        {
            string requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);

            string signature = data?.GetValueOrDefault("signature") ?? "";
            string signedHeaders = data?.GetValueOrDefault("signedHeaders") ?? "";

            return Results.Ok(new
            {
                Valid = !string.IsNullOrWhiteSpace(signature),
                Signature = signature,
                SignedHeaders = signedHeaders,
                Timestamp = DateTime.UtcNow
            });
        });

        app.MapGet("/demo/http-signature/generate-test", (IKeyGenerationService keyService) =>
        {
            var (privateKey, publicKey) = keyService.GenerateRSAKeyPair();

            return Results.Ok(new
            {
                KeyId = "test-key-" + Guid.NewGuid().ToString().Substring(0, 8),
                PrivateKey = privateKey,
                PublicKey = publicKey,
                ExampleHeaders = new Dictionary<string, string>
                {
                    { "(request-target)", "post /api/test" },
                    { "host", "localhost:8080" },
                    { "date", DateTime.UtcNow.ToString("R") },
                    { "digest", "SHA-256=47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=" },
                    { "content-length", "0" }
                }
            });
        });

        app.MapGet("/demo/federation/discover", async (IConfiguration config, HttpClient httpClient) =>
        {
            string actorUrl = config["Query:ActorUrl"] ?? "http://localhost:8080/users/test";

            return Results.Ok(new
            {
                ActorUrl = actorUrl,
                Endpoints = new
                {
                    Inbox = $"{actorUrl}/inbox",
                    Outbox = $"{actorUrl}/outbox",
                    Followers = $"{actorUrl}/followers",
                    Following = $"{actorUrl}/following"
                },
                Health = "online"
            });
        });

        app.MapGet("/demo/federation/webfinger", async (string resource, HttpClient httpClient) =>
        {
            if (string.IsNullOrWhiteSpace(resource))
                return Results.BadRequest("Resource parameter required");

            return Results.Ok(new
            {
                Subject = $"acct:{resource}",
                Links = new[]
                {
                    new { Rel = "self", Type = "application/activity+json", Href = $"http://localhost:8080/users/{resource}" },
                    new { Rel = "avatar", Type = "image/png", Href = "http://localhost:8080/avatar.png" },
                    new { Rel = "pubkey", Type = "key", Href = "http://localhost:8080/publickey" }
                }
            });
        });

        app.MapPost("/demo/service/simulate-receive", async (HttpContext context) =>
        {
            string requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();

            return Results.Ok(new
            {
                Success = true,
                Message = "Simulated activity received",
                ActivityId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow
            });
        });

        app.MapPost("/demo/service/simulate-send", async (HttpContext context) =>
        {
            string requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);

            string recipient = data?.GetValueOrDefault("recipient") ?? "";
            string activity = data?.GetValueOrDefault("activity") ?? "";

            return Results.Ok(new
            {
                Success = true,
                Recipient = recipient,
                ActivityId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow
            });
        });

        app.MapGet("/demo/protocol/validate", async (HttpContext context) =>
        {
            string activityType = context.Request.Query["type"].ToString();

            return Results.Ok(new
            {
                Valid = !string.IsNullOrWhiteSpace(activityType),
                ActivityType = activityType,
                Errors = Array.Empty<string>(),
                Warnings = Array.Empty<string>()
            });
        });

        app.MapGet("/demo/explorer/activities", async (string actorUrl, HttpClient httpClient) =>
        {
            if (string.IsNullOrWhiteSpace(actorUrl))
                return Results.BadRequest("Actor URL parameter required");

            try
            {
                string response = await httpClient.GetStringAsync(actorUrl);
                var activities = new
                {
                    ActorUrl = actorUrl,
                    Activities = new[]
                    {
                        new { Id = Guid.NewGuid().ToString(), Type = "Create", Content = "Sample activity from explorer" }
                    }
                };

                return Results.Ok(activities);
            }
            catch
            {
                return Results.Ok(new
                {
                    ActorUrl = actorUrl,
                    Activities = Array.Empty<object>()
                });
            }
        });

        app.MapGet("/demo/explorer/trace", async (string actorUrl, HttpClient httpClient) =>
        {
            if (string.IsNullOrWhiteSpace(actorUrl))
                return Results.BadRequest("Actor URL parameter required");

            return Results.Ok(new
            {
                ActorUrl = actorUrl,
                Chain = new[]
                {
                    new { Id = Guid.NewGuid().ToString(), Type = "Follow", Timestamp = DateTime.UtcNow.ToString("o") },
                    new { Id = Guid.NewGuid().ToString(), Type = "Accept", Timestamp = DateTime.UtcNow.ToString("o") }
                },
                TraceCompleted = true
            });
        });

        app.MapGet("/demo/federation/stats", () =>
        {
            return Results.Ok(new
            {
                Outbound = new
                {
                    Total = 12,
                    Pending = 3,
                    Processing = 1,
                    Completed = 8,
                    Failed = 1
                },
                Inbound = new
                {
                    Total = 45,
                    Pending = 2,
                    Processing = 0,
                    Completed = 40,
                    Failed = 3
                },
                SuccessRate = 97.5,
                AvgDeliveryTime = 2.3
            });
        });

        app.MapGet("/demo/federation/peers", () =>
        {
            return Results.Ok(new[]
            {
                new
                {
                    Domain = "example.com",
                    Online = true,
                    InboxUrl = "https://example.com/inbox",
                    Version = "ActivityPub 2.0"
                },
                new
                {
                    Domain = "mastodon.social",
                    Online = true,
                    InboxUrl = "https://mastodon.social/inbox",
                    Version = "ActivityPub 2.0"
                }
            });
        });

        app.MapGet("/demo/moderation/settings", () =>
        {
            return Results.Ok(new
            {
                BlockKeywords = new string[] { "spam", "scam" },
                BlockDomains = new string[] { "example-bad.com" },
                ShadowBanning = false,
                MrfRules = new[]
                {
                    new { Keyword = "spam", Action = "reject", Priority = 1 },
                    new { Keyword = "scam", Action = "transform", Priority = 2 }
                }
            });
        });

        app.MapPost("/demo/moderation/mrf/rules", async (HttpContext context) =>
        {
            string requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);

            string keyword = data?.GetValueOrDefault("keyword") ?? "";
            string action = data?.GetValueOrDefault("action") ?? "reject";

            return Results.Ok(new
            {
                Success = !string.IsNullOrWhiteSpace(keyword),
                Keyword = keyword,
                Action = action,
                Message = "MRF rule added successfully"
            });
        });

        app.MapDelete("/demo/moderation/mrf/rules", async (HttpContext context) =>
        {
            string keyword = context.Request.Query["keyword"].ToString();

            if (string.IsNullOrWhiteSpace(keyword))
                return Results.BadRequest("Keyword parameter required");

            return Results.Ok(new
            {
                Success = true,
                Keyword = keyword,
                Message = "MRF rule removed successfully"
            });
        });

        app.MapGet("/demo/moderation/logs", () =>
        {
            return Results.Ok(new
            {
                Logs = new[]
                {
                    new { Timestamp = DateTime.UtcNow.AddMinutes(-5).ToString("o"), Rule = "spam", Action = "reject", Details = "Blocked post containing spam keyword" },
                    new { Timestamp = DateTime.UtcNow.AddMinutes(-10).ToString("o"), Rule = "example-bad.com", Action = "transform", Details = "Modified post from blocked domain" },
                    new { Timestamp = DateTime.UtcNow.AddMinutes(-15).ToString("o"), Rule = "shadow-ban", Action = "accept", Details = "User shadow-banned" }
                }
            });
        });

        app.MapPost("/demo/moderation/apply", async (HttpContext context) =>
        {
            string requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody);

            string[]? blockKeywords = data?.GetValueOrDefault("BlockKeywords") as string[];
            string[]? blockDomains = data?.GetValueOrDefault("BlockDomains") as string[];
            bool? shadowBanning = data?.GetValueOrDefault("ShadowBanning") as bool? ?? false;

            return Results.Ok(new
            {
                Success = true,
                KeywordsBlocked = blockKeywords?.Length ?? 0,
                DomainsBlocked = blockDomains?.Length ?? 0,
                ShadowBanningEnabled = shadowBanning,
                Message = "Moderation settings applied"
            });
        });

        app.MapPost("/demo/moderation/save", async (HttpContext context) =>
        {
            string requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody);

            string[]? blockKeywords = data?.GetValueOrDefault("BlockKeywords") as string[];
            string[]? blockDomains = data?.GetValueOrDefault("BlockDomains") as string[];
            bool? shadowBanning = data?.GetValueOrDefault("ShadowBanning") as bool? ?? false;

            return Results.Ok(new
            {
                Success = true,
                KeywordsCount = blockKeywords?.Length ?? 0,
                DomainsCount = blockDomains?.Length ?? 0,
                ShadowBanning = shadowBanning,
                Message = "Moderation settings saved"
            });
        });

        app.MapPost("/demo/federation/retry", () =>
        {
            return Results.Ok(new
            {
                Success = true,
                Retried = 0,
                Message = "No failed items to retry"
            });
        });

        app.MapPost("/demo/federation/clear-failed", () =>
        {
            return Results.Ok(new
            {
                Success = true,
                Cleared = 0,
                Message = "No failed items to clear"
            });
        });

        app.MapGet("/demo/analytics/counts", () =>
        {
            return Results.Ok(new
            {
                TotalActivities = 1523,
                TodayActivities = 127,
                UniqueActors = 89,
                TotalPosts = 892,
                TotalReplies = 345,
                TotalBoosts = 156,
                TotalLikes = 123,
                Timestamp = DateTime.UtcNow
            });
        });

        app.MapGet("/demo/analytics/top-actors", () =>
        {
            return Results.Ok(new[]
            {
                new { Actor = "alice@example.com", Posts = 234, Replies = 56, Boosts = 23, Likes = 189 },
                new { Actor = "bob@social.net", Posts = 198, Replies = 89, Boosts = 45, Likes = 167 },
                new { Actor = "carol@fediverse.org", Posts = 167, Replies = 112, Boosts = 34, Likes = 145 },
                new { Actor = "dave@mastodon.social", Posts = 145, Replies = 67, Boosts = 56, Likes = 123 },
                new { Actor = "eve@pixelfed.net", Posts = 123, Replies = 45, Boosts = 23, Likes = 234 }
            });
        });

        app.MapGet("/demo/analytics/federation", () =>
        {
            return Results.Ok(new
            {
                TotalFollowers = 456,
                TotalFollowing = 312,
                ActivePeers = 23,
                FailedDeliveries = 7,
                AverageDeliveryTime = 1.8
            });
        });

        app.MapGet("/demo/analytics/trends", () =>
        {
            return Results.Ok(new
            {
                DailyActivities = new[]
                {
                    new { Day = "Mon", Activities = 189 },
                    new { Day = "Tue", Activities = 212 },
                    new { Day = "Wed", Activities = 198 },
                    new { Day = "Thu", Activities = 234 },
                    new { Day = "Fri", Activities = 256 },
                    new { Day = "Sat", Activities = 178 },
                    new { Day = "Sun", Activities = 145 }
                },
                TopDays = new[]
                {
                    new { Day = "Fri", Type = "Most Active", Value = 256 },
                    new { Day = "Sun", Type = "Least Active", Value = 145 }
                }
            });
        });

        app.MapGet("/demo/analytics/export", (string format = "json") =>
        {
            var data = new
            {
                GeneratedAt = DateTime.UtcNow.ToString("o"),
                Counts = new
                {
                    TotalActivities = 1523,
                    TodayActivities = 127,
                    UniqueActors = 89
                },
                TopActors = new[]
                {
                    new { Actor = "alice@example.com", Posts = 234 },
                    new { Actor = "bob@social.net", Posts = 198 }
                }
            };

            if (format?.ToLower() == "csv")
            {
                StringWriter csv = new();
                csv.WriteLine("Metric,Value,Timestamp");
                csv.WriteLine($"TotalActivities,{data.Counts.TotalActivities},{data.GeneratedAt}");
                csv.WriteLine($"TodayActivities,{data.Counts.TodayActivities},{data.GeneratedAt}");
                csv.WriteLine($"UniqueActors,{data.Counts.UniqueActors},{data.GeneratedAt}");
                return Results.Content(csv.ToString(), "text/csv");
            }

            return Results.Ok(data);
        });

        app.MapGet("/demo/metrics", (PerformanceMetricsService metrics) =>
        {
            var metricsData = metrics.GetMetrics();
            return Results.Ok(new
            {
                Timestamp = DateTime.UtcNow,
                TotalRequests = metricsData["totalRequests"],
                ErrorCount = metricsData["errorCount"],
                ProcessedItems = metricsData["processedItems"],
                Endpoints = metricsData["endpoints"],
                AverageEndpointTimes = metricsData["averageEndpointTimes"]
            });
        })
        .WithTags("Performance");

        app.MapGet("/security/rate-limit/info", (RateLimiterService rateLimiter, HttpContext context) =>
        {
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return Results.Ok(rateLimiter.GetRateLimitInfo(clientIp));
        })
        .WithTags("Security");

        app.MapPost("/security/rate-limit/configure", async (HttpContext context) =>
        {
            string requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody);

            int maxRequests = data?.GetValueOrDefault("maxRequests") is int mr ? mr : 100;
            int windowMinutes = data?.GetValueOrDefault("windowMinutes") is int wm ? wm : 1;

            return Results.Ok(new
            {
                Success = true,
                MaxRequests = maxRequests,
                WindowMinutes = windowMinutes,
                Timestamp = DateTime.UtcNow
            });
        })
        .WithTags("Security");

        app.MapGet("/security/tokens", (TokenService tokenService) =>
        {
            return Results.Ok(new
            {
                Tokens = tokenService.GetAllTokens().Select(t => new
                {
                    Token = t.Token,
                    ActorId = t.ActorId,
                    CreatedAt = t.CreatedAt,
                    ExpiresAt = t.ExpiresAt,
                    IsActive = t.IsActive,
                    Description = t.Description
                }).ToList(),
                Count = tokenService.GetAllTokens().Count
            });
        })
        .WithTags("Security");

        app.MapPost("/security/tokens/generate", async (HttpContext context, TokenService tokenService) =>
        {
            string requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);

            string actorId = data?.GetValueOrDefault("actorId") ?? "anonymous";
            string? description = data?.GetValueOrDefault("description");
            int expiryHours = data?.GetValueOrDefault("expiryHours") is string eh && int.TryParse(eh, out int hours) ? hours : 24;

            var token = tokenService.GenerateToken(actorId, description, expiryHours);

            return Results.Ok(new
            {
                Token = token,
                ActorId = actorId,
                Description = description,
                ExpiresAt = DateTime.UtcNow.AddHours(expiryHours),
                Success = true
            });
        })
        .WithTags("Security");

        app.MapPost("/security/tokens/validate", async (HttpContext context, TokenService tokenService) =>
        {
            string requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);

            string token = data?.GetValueOrDefault("token") ?? "";

            var result = tokenService.ValidateToken(token);

            return Results.Ok(new
            {
                Valid = result.IsValid,
                ActorId = result.ActorId,
                Message = result.ErrorMessage,
                Timestamp = DateTime.UtcNow
            });
        })
        .WithTags("Security");

        app.MapPost("/security/tokens/revoke", async (HttpContext context, TokenService tokenService) =>
        {
            string requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);

            string token = data?.GetValueOrDefault("token") ?? "";

            var success = tokenService.RevokeToken(token);

            return Results.Ok(new
            {
                Success = success,
                Message = success ? "Token revoked successfully" : "Token not found",
                Timestamp = DateTime.UtcNow
            });
        })
        .WithTags("Security");

        app.MapGet("/security/ip-filter/whitelist", (IPFilterService ipFilter) =>
        {
            return Results.Ok(new
            {
                Whitelist = ipFilter.GetWhitelist(),
                Count = ipFilter.GetWhitelist().Count
            });
        })
        .WithTags("Security");

        app.MapGet("/security/ip-filter/blacklist", (IPFilterService ipFilter) =>
        {
            return Results.Ok(new
            {
                Blacklist = ipFilter.GetBlacklist(),
                Count = ipFilter.GetBlacklist().Count
            });
        })
        .WithTags("Security");

        app.MapPost("/security/ip-filter/whitelist/add", async (HttpContext context, IPFilterService ipFilter) =>
        {
            string requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);

            string ipAddress = data?.GetValueOrDefault("ipAddress") ?? "";
            string? reason = data?.GetValueOrDefault("reason");

            ipFilter.AddToWhitelist(ipAddress, reason);

            return Results.Ok(new
            {
                Success = true,
                IP = ipAddress,
                Reason = reason,
                Timestamp = DateTime.UtcNow
            });
        })
        .WithTags("Security");

        app.MapDelete("/security/ip-filter/whitelist/remove", async (HttpContext context) =>
        {
            string ipAddress = context.Request.Query["ipAddress"].ToString();

            var ipFilter = context.RequestServices.GetRequiredService<IPFilterService>();
            ipFilter.RemoveFromWhitelist(ipAddress);

            return Results.Ok(new
            {
                Success = true,
                IP = ipAddress,
                Timestamp = DateTime.UtcNow
            });
        })
        .WithTags("Security");

        app.MapPost("/security/ip-filter/blacklist/add", async (HttpContext context, IPFilterService ipFilter) =>
        {
            string requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);

            string ipAddress = data?.GetValueOrDefault("ipAddress") ?? "";
            string? reason = data?.GetValueOrDefault("reason");

            ipFilter.AddToBlacklist(ipAddress, reason);

            return Results.Ok(new
            {
                Success = true,
                IP = ipAddress,
                Reason = reason,
                Timestamp = DateTime.UtcNow
            });
        })
        .WithTags("Security");

        app.MapDelete("/security/ip-filter/blacklist/remove", async (HttpContext context) =>
        {
            string ipAddress = context.Request.Query["ipAddress"].ToString();

            var ipFilter = context.RequestServices.GetRequiredService<IPFilterService>();
            ipFilter.RemoveFromBlacklist(ipAddress);

            return Results.Ok(new
            {
                Success = true,
                IP = ipAddress,
                Timestamp = DateTime.UtcNow
            });
        })
        .WithTags("Security");

        app.MapGet("/security/audit-logs", (AuditLogger auditLogger, string? eventType = null, int limit = 100) =>
        {
            var entries = auditLogger.GetEntries(eventType, null, null, limit);
            return Results.Ok(new
            {
                Entries = entries.Select(e => new
                {
                    Id = e.Id,
                    Timestamp = e.Timestamp,
                    EventType = e.EventType,
                    ActorId = e.ActorId,
                    ClientIP = e.ClientIP,
                    Endpoint = e.Endpoint,
                    Details = e.Details,
                    Success = e.Success
                }).ToList(),
                Count = entries.Count
            });
        })
        .WithTags("Security");

        app.MapGet("/security/audit-logs/login-attempts", (AuditLogger auditLogger, int limit = 100) =>
        {
            var entries = auditLogger.GetLoginAttempts(limit);
            return Results.Ok(new
            {
                Entries = entries.Select(e => new
                {
                    Id = e.Id,
                    Timestamp = e.Timestamp,
                    EventType = e.EventType,
                    ActorId = e.ActorId,
                    ClientIP = e.ClientIP
                }).ToList(),
                Count = entries.Count
            });
        })
        .WithTags("Security");

        app.MapGet("/security/audit-logs/statistics", (AuditLogger auditLogger) =>
        {
            var stats = auditLogger.GetStatistics();
            return Results.Ok(stats);
        })
        .WithTags("Security");

        app.MapPost("/security/login", async (HttpContext context, AuditLogger auditLogger) =>
        {
            string requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);

            string actorId = data?.GetValueOrDefault("actorId") ?? "";
            bool success = !string.IsNullOrWhiteSpace(actorId);

            auditLogger.Log("login_attempt", actorId,
                context.Connection.RemoteIpAddress?.ToString(),
                "/security/login",
                $"Login attempt: {actorId}",
                success);

            return Results.Ok(new
            {
                Success = success,
                ActorId = actorId,
                Message = success ? "Login successful" : "Invalid credentials",
                Timestamp = DateTime.UtcNow
            });
        })
        .WithTags("Security");

        app.MapPost("/security/rate-limit/hit", async (HttpContext context, AuditLogger auditLogger) =>
        {
            string clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            auditLogger.Log("rate_limit", null, clientIp, context.Request.Path.ToString(),
                "Rate limit exceeded", false);

            return Results.Ok(new
            {
                Success = true,
                Message = "Rate limit event logged",
                Timestamp = DateTime.UtcNow
            });
        })
        .WithTags("Security");

        app.MapGet(OAuth2Constants.AuthorizationEndpoint, async (string? clientId, string? redirectUri, string? responseType, string? state, string? codeChallenge, string? codeChallengeMethod, HashSet<string>? scopes, HttpContext context, IOAuth2Service oauthService, IMemoryCache cache) =>
        {
            if (string.IsNullOrEmpty(clientId))
            {
                return Results.BadRequest(new { error = "client_id is required" });
            }

            if (string.IsNullOrEmpty(redirectUri))
            {
                return Results.BadRequest(new { error = "redirect_uri is required" });
            }

            if (string.IsNullOrEmpty(responseType) || responseType != "code")
            {
                return Results.BadRequest(new { error = "response_type must be 'code'" });
            }

            var request = new AuthorizationRequest
            {
                ClientId = clientId,
                RedirectUri = redirectUri,
                ResponseType = responseType,
                State = state,
                CodeChallenge = codeChallenge,
                CodeChallengeMethod = codeChallengeMethod,
                Scopes = scopes
            };

            var actorId = GetActorIdFromRequest(context, cache);

            if (string.IsNullOrEmpty(actorId))
            {
                return Results.Redirect($"/login?redirectUri={Uri.EscapeDataString(redirectUri)}&clientId={Uri.EscapeDataString(clientId)}");
            }

            try
            {
                var response = await oauthService.CreateAuthorizationCodeAsync(request, actorId);

                var redirectUrl = $"{redirectUri}?code={response.Code}";
                if (!string.IsNullOrEmpty(response.State))
                {
                    redirectUrl += $"&state={response.State}";
                }

                return Results.Redirect(redirectUrl);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithTags("OAuth2");

        app.MapPost(OAuth2Constants.TokenEndpoint, async (TokenRequest request, HttpContext context, IOAuth2Service oauthService) =>
        {
            if (string.IsNullOrEmpty(request.GrantType))
            {
                return Results.BadRequest(new { error = "grant_type is required" });
            }

            try
            {
                var response = await oauthService.CreateTokenAsync(request);
                return Results.Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithTags("OAuth2");

        app.MapGet(OAuth2Constants.UserInfoEndpoint, async (HttpContext context, IOAuth2Service oauthService) =>
        {
            var authHeader = context.Request.Headers["Authorization"].ToString();
            string accessToken = null;

            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                accessToken = authHeader.Substring(7);
            }

            if (string.IsNullOrEmpty(accessToken))
            {
                return Results.StatusCode(401);
            }

            try
            {
                var userInfo = await oauthService.GetUserInfoAsync(accessToken);

                if (userInfo == null)
                {
                    return Results.StatusCode(401);
                }

                return Results.Ok(userInfo);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithTags("OAuth2");

        app.MapPost(OAuth2Constants.RevokeEndpoint, async (RevokeRequest request, string? clientId, HttpContext context, IOAuth2Service oauthService) =>
        {
            if (string.IsNullOrEmpty(request.Token))
            {
                return Results.BadRequest(new { error = "token is required" });
            }

            var success = await oauthService.RevokeTokenAsync(request.Token, clientId);

            return Results.Ok(new { success });
        })
        .WithTags("OAuth2");

        app.MapPost(OAuth2Constants.IntrospectEndpoint, async (IntrospectRequest request, IOAuth2Service oauthService) =>
        {
            if (string.IsNullOrEmpty(request.Token))
            {
                return Results.BadRequest(new { error = "token is required" });
            }

            var result = await oauthService.IntrospectTokenAsync(request.Token, request.ClientId, request.ClientSecret);
            return Results.Ok(result);
        })
        .WithTags("OAuth2");

        app.MapPost("/oauth2/pkce/challenge", async (string codeVerifier) =>
        {
            if (string.IsNullOrEmpty(codeVerifier))
            {
                return Results.BadRequest(new { error = "code_verifier is required" });
            }

            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(codeVerifier);
            var hash = sha256.ComputeHash(bytes);
            var codeChallenge = Convert.ToBase64String(hash).Replace("/", "_").Replace("+", "-").TrimEnd('=');

            return Results.Ok(new
            {
                code_challenge = codeChallenge,
                code_challenge_method = "S256"
            });
        })
        .WithTags("OAuth2");

        app.MapGet("/oauth2/scope/descriptions", () =>
        {
            return Results.Ok(OAuth2Scopes.ScopeDescriptions);
        })
        .WithTags("OAuth2");

        app.MapGet("/webhook/configs", async (ActivityPubDbContext db, string? actorId, string? eventType) =>
        {
            if (string.IsNullOrEmpty(actorId))
                return Results.BadRequest("actorId is required");

            var query = db.WebhookConfigs.Where(c => c.ActorId == actorId);
            if (!string.IsNullOrEmpty(eventType))
            {
                query = query.Where(c => c.EventType == eventType);
            }

            var configs = await query.ToListAsync();
            return Results.Ok(configs);
        })
        .WithTags("Webhooks");

        app.MapGet("/webhook/configs/{id:int}", async (int id, ActivityPubDbContext db) =>
        {
            var config = await db.WebhookConfigs.FindAsync(id);
            if (config == null)
                return Results.NotFound();

            return Results.Ok(config);
        })
        .WithTags("Webhooks");

        app.MapPost("/webhook/configs", async (HttpContext context, ActivityPubDbContext db) =>
        {
            string requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody);

            string actorId = data?.GetValueOrDefault("actorId")?.ToString() ?? "";
            string eventType = data?.GetValueOrDefault("eventType")?.ToString() ?? "All";
            string endpointUrl = data?.GetValueOrDefault("endpointUrl")?.ToString() ?? "";
            string httpMethod = data?.GetValueOrDefault("httpMethod")?.ToString() ?? "POST";
            bool enabled = data?.GetValueOrDefault("enabled") is bool e ? e : true;
            string? secretKey = data?.GetValueOrDefault("secretKey")?.ToString();
            int maxRetries = data?.GetValueOrDefault("maxRetries") is int mr ? mr : 3;
            int retryDelaySeconds = data?.GetValueOrDefault("retryDelaySeconds") is int rds ? rds : 60;
            bool useExponentialBackoff = data?.GetValueOrDefault("useExponentialBackoff") is bool ueb ? ueb : true;

            var config = new WebhookConfigEntity
            {
                ActorId = actorId,
                EventType = eventType,
                EndpointUrl = endpointUrl,
                HttpMethod = httpMethod,
                Enabled = enabled,
                SecretKey = secretKey,
                MaxRetries = maxRetries,
                RetryDelaySeconds = retryDelaySeconds,
                UseExponentialBackoff = useExponentialBackoff,
                DeliveryMethod = httpMethod switch
                {
                    "POST" => WebhookDeliveryMethod.HttpPost,
                    "PUT" => WebhookDeliveryMethod.HttpPut,
                    _ => WebhookDeliveryMethod.HttpPost
                }
            };

            await db.WebhookConfigs.AddAsync(config);
            await db.SaveChangesAsync();

            return Results.Created($"/webhook/configs/{config.Id}", config);
        })
        .WithTags("Webhooks");

        app.MapPut("/webhook/configs/{id:int}", async (int id, HttpContext context, ActivityPubDbContext db) =>
        {
            string requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody);

            var config = await db.WebhookConfigs.FindAsync(id);
            if (config == null)
                return Results.NotFound();

            if (data?.GetValueOrDefault("endpointUrl") is string eu) config.EndpointUrl = eu;
            if (data?.GetValueOrDefault("httpMethod") is string hm) config.HttpMethod = hm;
            if (data?.GetValueOrDefault("enabled") is bool e) config.Enabled = e;
            if (data?.GetValueOrDefault("secretKey") is string sk) config.SecretKey = sk;
            if (data?.GetValueOrDefault("maxRetries") is int mr) config.MaxRetries = mr;
            if (data?.GetValueOrDefault("retryDelaySeconds") is int rds) config.RetryDelaySeconds = rds;
            if (data?.GetValueOrDefault("useExponentialBackoff") is bool ueb) config.UseExponentialBackoff = ueb;
            if (data?.GetValueOrDefault("eventType") is string et) config.EventType = et;

            config.UpdatedAt = DateTime.UtcNow;

            db.WebhookConfigs.Update(config);
            await db.SaveChangesAsync();

            return Results.Ok(config);
        })
        .WithTags("Webhooks");

        app.MapDelete("/webhook/configs/{id:int}", async (int id, ActivityPubDbContext db) =>
        {
            var config = await db.WebhookConfigs.FindAsync(id);
            if (config == null)
                return Results.NotFound();

            db.WebhookConfigs.Remove(config);
            await db.SaveChangesAsync();

            return Results.Ok(new { success = true, id });
        })
        .WithTags("Webhooks");

        app.MapPost("/webhook/process", async (ActivityPubDbContext db) =>
        {
            var deliveryService = app.Services.GetRequiredService<IWebhookDeliveryService>();
            await deliveryService.ProcessPendingDeliveriesAsync();

            return Results.Ok(new
            {
                Success = true,
                Message = "Webhook deliveries processed"
            });
        })
        .WithTags("Webhooks");

        app.MapGet("/webhook/deliveries", async (ActivityPubDbContext db, string? configId, WebhookDeliveryStatus? status) =>
        {
            var query = db.WebhookDeliveries.AsQueryable();

            if (!string.IsNullOrEmpty(configId))
            {
                query = query.Where(d => d.ConfigId == configId);
            }

            if (status.HasValue)
            {
                query = query.Where(d => d.Status == status.Value);
            }

            var deliveries = await query
                .OrderByDescending(d => d.CreatedAt)
                .Take(100)
                .ToListAsync();

            return Results.Ok(deliveries);
        })
        .WithTags("Webhooks");

        app.MapGet("/webhook/deliveries/{id}", async (string id, ActivityPubDbContext db) =>
        {
            var delivery = await db.WebhookDeliveries.FindAsync(id);
            if (delivery == null)
                return Results.NotFound();

            return Results.Ok(delivery);
        })
        .WithTags("Webhooks");
    }

    private static string GetActorIdFromRequest(HttpContext context, IMemoryCache cache)
    {
        var sessionId = context.Request.Cookies["session_id"];
        if (!string.IsNullOrEmpty(sessionId))
        {
            return cache.Get<string>($"user_{sessionId}") ?? string.Empty;
        }
        return string.Empty;
    }
}
