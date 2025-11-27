namespace OpenTicket.Ddd.Application.IntegrationEvents.Outbox;

/// <summary>
/// Configuration options for the outbox processor.
/// </summary>
public class OutboxOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Outbox";

    /// <summary>
    /// Interval between outbox processing runs.
    /// Default: 5 seconds.
    /// </summary>
    public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum number of messages to process per batch.
    /// Default: 100.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum number of retry attempts before marking as failed.
    /// Default: 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// How long to keep published messages before cleanup.
    /// Default: 7 days.
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Whether to enable the outbox processor background service.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
