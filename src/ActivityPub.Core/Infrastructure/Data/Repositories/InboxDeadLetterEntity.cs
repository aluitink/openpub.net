namespace ActivityPub.Core.Repositories;

/// <summary>
/// A row in the inbound dead-letter queue: an activity that a remote server
/// delivered to our inbox, which kept failing to process until it exhausted
/// its retry budget (see <see cref="InboxProcessingOptions"/>). The row keeps
/// the raw payload so the failure can be inspected and re-processed later
/// without the remote server having to redeliver it.
/// </summary>
public class InboxDeadLetterEntity
{
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The ActivityPub activity ID (URL) when it could be extracted from the
    /// payload; otherwise the (synthetic) <c>unknown-{Id}</c> marker.
    /// </summary>
    public required string ActivityId { get; set; }

    /// <summary>
    /// The exact raw JSON the remote server sent, so the item can be replayed
    /// through the pipeline unchanged.
    /// </summary>
    public required string RawJson { get; set; }

    /// <summary>
    /// The username whose inbox endpoint the activity was POSTed to.
    /// </summary>
    public required string Username { get; set; }

    public InboxDeadLetterStatus Status { get; set; }

    /// <summary>
    /// Number of processing attempts made before the item was dead-lettered.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// The error message from the last (decisive) attempt.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// When the last processing attempt ran, or null when the item has never
    /// been re-processed.
    /// </summary>
    public DateTime? LastAttemptAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum InboxDeadLetterStatus
{
    /// <summary>
    /// In the dead-letter queue, awaiting inspection or re-processing.
    /// </summary>
    DeadLettered = 0,

    /// <summary>
    /// A re-processing attempt is in flight.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Terminal: a re-processing attempt failed again and the item is left in
    /// the DLQ for manual inspection.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// Terminal: re-processing succeeded; the activity was stored and
    /// distributed.
    /// </summary>
    Replayed = 3
}
