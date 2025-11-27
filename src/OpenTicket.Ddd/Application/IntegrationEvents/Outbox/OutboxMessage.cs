using OpenTicket.Ddd.Domain;

namespace OpenTicket.Ddd.Application.IntegrationEvents.Outbox;

/// <summary>
/// Represents an outbox message for reliable event publishing.
/// Events are first stored in the outbox, then published to the message broker.
/// </summary>
public class OutboxMessage : AggregateRoot
{
    private OutboxMessage() { } // EF Core constructor

    /// <summary>
    /// The event ID from the integration event.
    /// </summary>
    public Guid EventId { get; private set; }

    /// <summary>
    /// The type name of the event for deserialization.
    /// </summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>
    /// The aggregate ID for partition routing.
    /// </summary>
    public string AggregateId { get; private set; } = string.Empty;

    /// <summary>
    /// Serialized event payload (JSON).
    /// </summary>
    public string Payload { get; private set; } = string.Empty;

    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// When the message was published (null if not yet published).
    /// </summary>
    public DateTime? PublishedAt { get; private set; }

    /// <summary>
    /// Number of times publishing has been attempted.
    /// </summary>
    public int RetryCount { get; private set; }

    /// <summary>
    /// Last error message if publishing failed.
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// The current status of this outbox message.
    /// </summary>
    public OutboxMessageStatus Status { get; private set; } = OutboxMessageStatus.Pending;

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string? CorrelationId { get; private set; }

    /// <summary>
    /// Creates a new outbox message.
    /// </summary>
    public static OutboxMessage Create(
        Guid eventId,
        string eventType,
        string aggregateId,
        string payload,
        string? correlationId = null)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            EventType = eventType,
            AggregateId = aggregateId,
            Payload = payload,
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending
        };
    }

    /// <summary>
    /// Marks the message as being processed.
    /// </summary>
    public void MarkAsProcessing()
    {
        Status = OutboxMessageStatus.Processing;
    }

    /// <summary>
    /// Marks the message as successfully published.
    /// </summary>
    public void MarkAsPublished()
    {
        Status = OutboxMessageStatus.Published;
        PublishedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the message as failed after max retries.
    /// </summary>
    public void MarkAsFailed(string error)
    {
        Status = OutboxMessageStatus.Failed;
        LastError = error;
    }

    /// <summary>
    /// Increments the retry count and resets to pending for retry.
    /// </summary>
    public void IncrementRetry(string error)
    {
        RetryCount++;
        LastError = error;
        Status = OutboxMessageStatus.Pending;
    }
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
