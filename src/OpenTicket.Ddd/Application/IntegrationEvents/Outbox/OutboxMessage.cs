namespace OpenTicket.Ddd.Application.IntegrationEvents.Outbox;

/// <summary>
/// Represents an outbox message for reliable event publishing.
/// Events are first stored in the outbox, then published to the message broker.
/// </summary>
public class OutboxMessage
{
    /// <summary>
    /// Unique identifier for this outbox message.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The event ID from the integration event.
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// The type name of the event for deserialization.
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// The aggregate ID for partition routing.
    /// </summary>
    public string AggregateId { get; init; } = string.Empty;

    /// <summary>
    /// Serialized event payload (JSON).
    /// </summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the message was published (null if not yet published).
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// Number of times publishing has been attempted.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Last error message if publishing failed.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// The current status of this outbox message.
    /// </summary>
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Status of an outbox message.
/// </summary>
public enum OutboxMessageStatus
{
    /// <summary>
    /// Message is waiting to be published.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Message is currently being processed.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Message has been successfully published.
    /// </summary>
    Published = 2,

    /// <summary>
    /// Message publishing has failed after max retries.
    /// </summary>
    Failed = 3
}
