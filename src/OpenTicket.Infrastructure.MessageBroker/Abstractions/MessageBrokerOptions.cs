namespace OpenTicket.Infrastructure.MessageBroker.Abstractions;

/// <summary>
/// Enum for selecting message broker provider.
/// </summary>
public enum MessageBrokerOption
{
    Redis,
    Nats,
    RabbitMq,
    Kafka
}

/// <summary>
/// Configuration options for the message broker.
/// </summary>
public class MessageBrokerOptions
{
    public const string SectionName = "MessageBroker";

    /// <summary>
    /// The number of partitions for distributing messages.
    /// Default is 64. Increase for higher throughput scenarios.
    /// </summary>
    public int PartitionCount { get; set; } = 64;

    /// <summary>
    /// Maximum number of messages to process in a batch.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Timeout for waiting on messages when queue is empty.
    /// </summary>
    public TimeSpan PollTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum number of retry attempts for failed messages.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Time-to-live for messages. Null means messages never expire.
    /// </summary>
    public TimeSpan? MessageTtl { get; set; }

    /// <summary>
    /// Whether to enable message deduplication.
    /// </summary>
    public bool EnableDeduplication { get; set; } = false;

    /// <summary>
    /// Window for deduplication check.
    /// </summary>
    public TimeSpan DeduplicationWindow { get; set; } = TimeSpan.FromMinutes(5);
}
