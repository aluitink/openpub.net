using ActivityPub.Core.Caching;
using ActivityPub.Core.Implementations;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Options;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ActivityPub.Tests.Services;

/// <summary>
/// Phase 38 Task 5 — inbound inbox error handling with retry and a dead-letter
/// queue. Exercises <see cref="SharedInboxService.ProcessAndDistributeActivityAsync"/>
/// (the 3-arg overload that accepts the raw payload) against the in-memory
/// repository, verifying the success path, the retry budget, the terminal
/// dead-letter state, replay of dead-lettered items, and retention pruning.
/// </summary>
public class InboxDeadLetterTests
{
    private const string InboxUser = "testuser";

    private static Activity CreateActivity(string id, string type = "Create", string? actor = "https://remote.example/users/alice")
    {
        return new Activity
        {
            Id = id,
            Type = type,
            Actor = actor,
            Object = "https://remote.example/notes/1",
            Published = DateTime.UtcNow,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" }
        };
    }

    private static InboxProcessingOptions InboxOptions(int maxAttempts = 3, int baseDelaySeconds = 0)
    {
        return new InboxProcessingOptions
        {
            Enabled = true,
            MaxAttempts = maxAttempts,
            BaseRetryDelaySeconds = baseDelaySeconds,
            UseExponentialBackoff = true,
            MaxRetryDelaySeconds = 1,
            DlqRetentionDays = 7
        };
    }

    private static SharedInboxService CreateService(
        Mock<IActivityPubRepository> repo,
        InboxProcessingOptions inboxOptions)
    {
        var options = new ActivityPubOptions { InboxProcessing = inboxOptions };
        return new SharedInboxService(
            repo.Object,
            Mock.Of<IOutboundActivityService>(),
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IFederationCache>(),
            Mock.Of<ILogger<SharedInboxService>>(),
            Options.Create(options));
    }

    // ------------------------------------------------------------------
    // Success path
    // ------------------------------------------------------------------

    [Fact]
    public async Task ValidActivity_ProcessesAndReturnsTrue_NoDeadLetterRow()
    {
        var repo = new Mock<IActivityPubRepository>();
        repo.Setup(r => r.HasSeenActivityAsync(It.IsAny<string>())).ReturnsAsync(false);
        repo.Setup(r => r.MarkActivityAsSeenAsync(It.IsAny<string>())).ReturnsAsync(true);
        repo.Setup(r => r.SaveActivityAsync(It.IsAny<Activity>())).ReturnsAsync(true);
        repo.Setup(r => r.GetUniqueFollowerIdsAsync(It.IsAny<string>())).ReturnsAsync(new List<string>());
        repo.Setup(r => r.QueueSharedInboxDeliveryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        var service = CreateService(repo, InboxOptions());
        var activity = CreateActivity("act-success");

        var result = await service.ProcessAndDistributeActivityAsync(InboxUser, activity, rawJson: "{\"id\":\"act-success\"}");

        Assert.True(result);
        repo.Verify(r => r.SaveActivityAsync(activity), Times.Once);
        repo.Verify(r => r.AddInboxDeadLetterAsync(It.IsAny<InboxDeadLetterEntity>()), Times.Never);
    }

    [Fact]
    public async Task DuplicateActivity_ReturnsTrue_AsSuccess()
    {
        var repo = new Mock<IActivityPubRepository>();
        repo.Setup(r => r.HasSeenActivityAsync("act-dup")).ReturnsAsync(true);

        var service = CreateService(repo, InboxOptions());

        var result = await service.ProcessAndDistributeActivityAsync(InboxUser, CreateActivity("act-dup"));

        Assert.True(result);
        repo.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Never);
        repo.Verify(r => r.AddInboxDeadLetterAsync(It.IsAny<InboxDeadLetterEntity>()), Times.Never);
    }

    // --- End-to-end dedup against the REAL repository --------------------
    // The test above proves the *service* skips a save when the repo says
    // "already seen," but it mocks the repo. This drives the real
    // InMemoryActivityPubRepository through the same public entry point twice
    // with the identical activity and proves the federation-health guarantee:
    // a redelivered activity is stored EXACTLY once (no duplicates), and both
    // deliveries are accepted (no loss / no spurious rejection).
    [Fact]
    public async Task DuplicateInboundActivity_RealRepository_StoredExactlyOnce()
    {
        var repo = new InMemoryActivityPubRepository();
        var service = new SharedInboxService(
            repo,
            Mock.Of<IOutboundActivityService>(),
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IFederationCache>(),
            Mock.Of<ILogger<SharedInboxService>>(),
            Options.Create(new ActivityPubOptions { InboxProcessing = InboxOptions() }));

        var activity = CreateActivity("https://remote.example/notes/42");
        var first = await service.ProcessAndDistributeActivityAsync(InboxUser, activity);
        // The remote redelivers the very same activity (e.g. at-least-once
        // transport, or a retry after a flaky network).
        var second = await service.ProcessAndDistributeActivityAsync(InboxUser, activity);

        // Both deliveries are accepted (the duplicate is a no-op success, not
        // an error), and the activity ends up stored exactly once.
        Assert.True(first);
        Assert.True(second);
        var ids = await repo.GetAllActivityIdsAsync();
        Assert.Single(ids);
        Assert.Equal("https://remote.example/notes/42", Assert.Single(ids).ToString());
        var stored = await repo.GetActivityAsync("https://remote.example/notes/42");
        Assert.NotNull(stored);
        Assert.True(await repo.HasSeenActivityAsync("https://remote.example/notes/42"));
    }

    // ------------------------------------------------------------------
    // Client-side rejections (no retry, no DLQ — the payload itself is bad)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(null, "Create", "https://remote.example/users/alice")]   // no id
    [InlineData("act-x", null, "https://remote.example/users/alice")]    // no type
    [InlineData("act-x", "Create", null)]                                // no actor
    [InlineData("act-x", "BogusType", "https://remote.example/users/alice")] // unsupported type
    public async Task InvalidActivity_IsRejected_WithoutRetryOrDeadLetter(string? id, string? type, string? actor)
    {
        var repo = new Mock<IActivityPubRepository>();
        var service = CreateService(repo, InboxOptions(maxAttempts: 3));
        var activity = new Activity { Id = id, Type = type, Actor = actor };

        var result = await service.ProcessAndDistributeActivityAsync(InboxUser, activity);

        Assert.False(result);
        repo.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Never);
        repo.Verify(r => r.AddInboxDeadLetterAsync(It.IsAny<InboxDeadLetterEntity>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Retry budget + terminal dead-letter state
    // ------------------------------------------------------------------

    [Fact]
    public async Task PersistentlyFailingActivity_RetriesThenDeadLetters_ReturnsTrue()
    {
        var repo = new Mock<IActivityPubRepository>();
        repo.Setup(r => r.HasSeenActivityAsync(It.IsAny<string>())).ReturnsAsync(false);
        // The decisive failure: every processing attempt throws.
        repo.Setup(r => r.MarkActivityAsSeenAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("simulated db failure"));
        repo.Setup(r => r.SaveActivityAsync(It.IsAny<Activity>()))
            .ThrowsAsync(new InvalidOperationException("simulated db failure"));
        repo.Setup(r => r.AddInboxDeadLetterAsync(It.IsAny<InboxDeadLetterEntity>()))
            .ReturnsAsync((InboxDeadLetterEntity e) => e);

        var service = CreateService(repo, InboxOptions(maxAttempts: 3, baseDelaySeconds: 0));
        var activity = CreateActivity("act-fail");

        var result = await service.ProcessAndDistributeActivityAsync(InboxUser, activity, rawJson: "{\"id\":\"act-fail\"}");

        // The activity is reported as accepted (true) so the remote server
        // stops redelivering — it now sits in the dead-letter queue.
        Assert.True(result);

        // All three attempts were made.
        repo.Verify(r => r.MarkActivityAsSeenAsync("act-fail"), Times.Exactly(3));
        repo.Verify(r => r.AddInboxDeadLetterAsync(It.IsAny<InboxDeadLetterEntity>()), Times.Once);
        var dlq = repo.Invocations
            .First(i => i.Method.Name == nameof(IActivityPubRepository.AddInboxDeadLetterAsync))
            .Arguments[0] as InboxDeadLetterEntity;
        Assert.NotNull(dlq);
        Assert.Equal("act-fail", dlq!.ActivityId);
        Assert.Equal(InboxUser, dlq.Username);
        Assert.Equal("{\"id\":\"act-fail\"}", dlq.RawJson);
        Assert.Equal(InboxDeadLetterStatus.DeadLettered, dlq.Status);
        Assert.Equal(3, dlq.AttemptCount);
        Assert.Equal("simulated db failure", dlq.FailureReason);
    }

    [Fact]
    public async Task FailingActivity_MaxAttemptsOne_DeadLettersImmediately()
    {
        var repo = new Mock<IActivityPubRepository>();
        repo.Setup(r => r.HasSeenActivityAsync(It.IsAny<string>())).ReturnsAsync(false);
        repo.Setup(r => r.MarkActivityAsSeenAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("boom"));
        repo.Setup(r => r.AddInboxDeadLetterAsync(It.IsAny<InboxDeadLetterEntity>()))
            .ReturnsAsync((InboxDeadLetterEntity e) => e);

        var service = CreateService(repo, InboxOptions(maxAttempts: 1));

        var result = await service.ProcessAndDistributeActivityAsync(InboxUser, CreateActivity("act-once"));

        Assert.True(result);
        repo.Verify(r => r.MarkActivityAsSeenAsync("act-once"), Times.Once);
        var dlq = repo.Invocations
            .First(i => i.Method.Name == nameof(IActivityPubRepository.AddInboxDeadLetterAsync))
            .Arguments[0] as InboxDeadLetterEntity;
        Assert.Equal(1, dlq!.AttemptCount);
    }

    [Fact]
    public async Task FailingActivity_RetriesSucceed_ReturnsTrue_NoDeadLetter()
    {
        var repo = new Mock<IActivityPubRepository>();
        repo.Setup(r => r.HasSeenActivityAsync(It.IsAny<string>())).ReturnsAsync(false);
        // First attempt fails, second succeeds.
        repo.SetupSequence(r => r.MarkActivityAsSeenAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("transient"))
            .ReturnsAsync(true);
        repo.Setup(r => r.SaveActivityAsync(It.IsAny<Activity>())).ReturnsAsync(true);
        repo.Setup(r => r.GetUniqueFollowerIdsAsync(It.IsAny<string>())).ReturnsAsync(new List<string>());

        var service = CreateService(repo, InboxOptions(maxAttempts: 3, baseDelaySeconds: 0));

        var result = await service.ProcessAndDistributeActivityAsync(InboxUser, CreateActivity("act-recover"));

        Assert.True(result);
        repo.Verify(r => r.MarkActivityAsSeenAsync("act-recover"), Times.Exactly(2));
        repo.Verify(r => r.SaveActivityAsync(It.IsAny<Activity>()), Times.Once);
        repo.Verify(r => r.AddInboxDeadLetterAsync(It.IsAny<InboxDeadLetterEntity>()), Times.Never);
    }

    [Fact]
    public async Task FailingActivity_WhenDisabled_ReturnsFalse_NoDeadLetter()
    {
        var repo = new Mock<IActivityPubRepository>();
        repo.Setup(r => r.HasSeenActivityAsync(It.IsAny<string>())).ReturnsAsync(false);
        repo.Setup(r => r.MarkActivityAsSeenAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("boom"));

        var options = InboxOptions(maxAttempts: 3);
        options.Enabled = false;
        var service = CreateService(repo, options);

        var result = await service.ProcessAndDistributeActivityAsync(InboxUser, CreateActivity("act-legacy"));

        Assert.False(result);
        repo.Verify(r => r.AddInboxDeadLetterAsync(It.IsAny<InboxDeadLetterEntity>()), Times.Never);
    }

    [Fact]
    public async Task DeadLetterWriteFailure_IsSwallowed_AndStillReturnsTrue()
    {
        var repo = new Mock<IActivityPubRepository>();
        repo.Setup(r => r.HasSeenActivityAsync(It.IsAny<string>())).ReturnsAsync(false);
        repo.Setup(r => r.MarkActivityAsSeenAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("db down"));
        repo.Setup(r => r.AddInboxDeadLetterAsync(It.IsAny<InboxDeadLetterEntity>()))
            .ThrowsAsync(new Exception("dlq also down"));

        var service = CreateService(repo, InboxOptions(maxAttempts: 1));

        var result = await service.ProcessAndDistributeActivityAsync(InboxUser, CreateActivity("act-nodlq"));

        Assert.True(result);
    }

    // ------------------------------------------------------------------
    // Replay of dead-lettered items
    // ------------------------------------------------------------------

    [Fact]
    public async Task ProcessInboxDeadLetters_ReplaysEligibleItem_MarksReplayed()
    {
        var repo = new Mock<IActivityPubRepository>();
        var dlqRow = new InboxDeadLetterEntity
        {
            Id = "dlq-1",
            ActivityId = "act-replay",
            RawJson = System.Text.Json.JsonSerializer.Serialize(CreateActivity("act-replay")),
            Username = InboxUser,
            Status = InboxDeadLetterStatus.DeadLettered,
            AttemptCount = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        repo.Setup(r => r.GetReprocessableInboxDeadLettersAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<InboxDeadLetterEntity> { dlqRow });
        repo.Setup(r => r.UpdateInboxDeadLetterAsync(It.IsAny<InboxDeadLetterEntity>())).ReturnsAsync(true);
        repo.Setup(r => r.HasSeenActivityAsync(It.IsAny<string>())).ReturnsAsync(false);
        repo.Setup(r => r.MarkActivityAsSeenAsync(It.IsAny<string>())).ReturnsAsync(true);
        repo.Setup(r => r.SaveActivityAsync(It.IsAny<Activity>())).ReturnsAsync(true);
        repo.Setup(r => r.GetUniqueFollowerIdsAsync(It.IsAny<string>())).ReturnsAsync(new List<string>());

        var service = CreateService(repo, InboxOptions());

        var replayed = await service.ProcessInboxDeadLettersAsync();

        Assert.Equal(1, replayed);
        Assert.Equal(InboxDeadLetterStatus.Replayed, dlqRow.Status);
        Assert.Null(dlqRow.FailureReason);
        repo.Verify(r => r.UpdateInboxDeadLetterAsync(dlqRow), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessInboxDeadLetters_FailingReplay_MarksFailed()
    {
        var repo = new Mock<IActivityPubRepository>();
        var dlqRow = new InboxDeadLetterEntity
        {
            Id = "dlq-2",
            ActivityId = "act-replay-fail",
            RawJson = System.Text.Json.JsonSerializer.Serialize(CreateActivity("act-replay-fail")),
            Username = InboxUser,
            Status = InboxDeadLetterStatus.DeadLettered,
            AttemptCount = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        repo.Setup(r => r.GetReprocessableInboxDeadLettersAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<InboxDeadLetterEntity> { dlqRow });
        repo.Setup(r => r.UpdateInboxDeadLetterAsync(It.IsAny<InboxDeadLetterEntity>())).ReturnsAsync(true);
        repo.Setup(r => r.HasSeenActivityAsync(It.IsAny<string>())).ReturnsAsync(false);
        repo.Setup(r => r.MarkActivityAsSeenAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("still broken"));

        var service = CreateService(repo, InboxOptions());

        var replayed = await service.ProcessInboxDeadLettersAsync();

        Assert.Equal(0, replayed);
        Assert.Equal(InboxDeadLetterStatus.Failed, dlqRow.Status);
        Assert.Equal("still broken", dlqRow.FailureReason);
        Assert.Equal(4, dlqRow.AttemptCount);
    }

    [Fact]
    public async Task ProcessInboxDeadLetters_MalformedJson_MarksFailed()
    {
        var repo = new Mock<IActivityPubRepository>();
        var dlqRow = new InboxDeadLetterEntity
        {
            Id = "dlq-3",
            ActivityId = "act-malformed",
            RawJson = "not-json-at-all",
            Username = InboxUser,
            Status = InboxDeadLetterStatus.DeadLettered,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        repo.Setup(r => r.GetReprocessableInboxDeadLettersAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<InboxDeadLetterEntity> { dlqRow });
        repo.Setup(r => r.UpdateInboxDeadLetterAsync(It.IsAny<InboxDeadLetterEntity>())).ReturnsAsync(true);

        var service = CreateService(repo, InboxOptions());

        var replayed = await service.ProcessInboxDeadLettersAsync();

        Assert.Equal(0, replayed);
        Assert.Equal(InboxDeadLetterStatus.Failed, dlqRow.Status);
        Assert.Equal("Failed to deserialize dead-lettered activity", dlqRow.FailureReason);
    }

    // ------------------------------------------------------------------
    // Repository: dedup + pruning semantics
    // ------------------------------------------------------------------

    [Fact]
    public async Task InMemory_AddDeadLetter_DedupesByActivityAndUsername()
    {
        var repo = new InMemoryActivityPubRepository();
        var first = new InboxDeadLetterEntity
        {
            ActivityId = "act-d",
            RawJson = "{\"v\":1}",
            Username = InboxUser,
            Status = InboxDeadLetterStatus.DeadLettered,
            AttemptCount = 3
        };

        var stored1 = await repo.AddInboxDeadLetterAsync(first);
        Assert.False(string.IsNullOrEmpty(stored1.Id));

        var second = new InboxDeadLetterEntity
        {
            Id = "other-id",
            ActivityId = "act-d",
            RawJson = "{\"v\":2}",
            Username = InboxUser,
            Status = InboxDeadLetterStatus.DeadLettered,
            AttemptCount = 3
        };
        var stored2 = await repo.AddInboxDeadLetterAsync(second);

        // Same row is returned (no duplicate), with the refreshed payload.
        Assert.Equal(stored1.Id, stored2.Id);
        Assert.Equal("{\"v\":2}", stored2.RawJson);

        var all = await repo.GetInboxDeadLettersAsync(100);
        Assert.Single(all, d => d.ActivityId == "act-d");
    }

    [Fact]
    public async Task InMemory_AddDeadLetter_DifferentUsername_CreatesSeparateRow()
    {
        var repo = new InMemoryActivityPubRepository();
        await repo.AddInboxDeadLetterAsync(new InboxDeadLetterEntity
        {
            ActivityId = "act-e",
            RawJson = "{}",
            Username = "user-a",
            Status = InboxDeadLetterStatus.DeadLettered
        });
        await repo.AddInboxDeadLetterAsync(new InboxDeadLetterEntity
        {
            ActivityId = "act-e",
            RawJson = "{}",
            Username = "user-b",
            Status = InboxDeadLetterStatus.DeadLettered
        });

        var all = await repo.GetInboxDeadLettersAsync(100);
        Assert.Equal(2, all.Count(d => d.ActivityId == "act-e"));
    }

    [Fact]
    public async Task InMemory_GetReprocessable_ExcludesNonDeadLetteredRows()
    {
        var repo = new InMemoryActivityPubRepository();
        await repo.AddInboxDeadLetterAsync(new InboxDeadLetterEntity
        {
            ActivityId = "act-a",
            RawJson = "{}",
            Username = InboxUser,
            Status = InboxDeadLetterStatus.DeadLettered
        });

        var row = (await repo.GetInboxDeadLettersAsync(10)).Single();
        row.Status = InboxDeadLetterStatus.Replayed;
        await repo.UpdateInboxDeadLetterAsync(row);

        var eligible = await repo.GetReprocessableInboxDeadLettersAsync(100);
        Assert.Empty(eligible);
    }

    [Fact]
    public async Task InMemory_Prune_RemovesOnlyOldRows()
    {
        var repo = new InMemoryActivityPubRepository();
        await repo.AddInboxDeadLetterAsync(new InboxDeadLetterEntity
        {
            ActivityId = "act-old",
            RawJson = "{}",
            Username = InboxUser,
            Status = InboxDeadLetterStatus.DeadLettered,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-30)
        });
        await repo.AddInboxDeadLetterAsync(new InboxDeadLetterEntity
        {
            ActivityId = "act-new",
            RawJson = "{}",
            Username = InboxUser,
            Status = InboxDeadLetterStatus.DeadLettered,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        });

        var pruned = await repo.PruneInboxDeadLettersAsync(DateTime.UtcNow.AddDays(-7));

        Assert.Equal(1, pruned);
        var remaining = await repo.GetInboxDeadLettersAsync(100);
        Assert.Single(remaining, d => d.ActivityId == "act-new");
    }
}
