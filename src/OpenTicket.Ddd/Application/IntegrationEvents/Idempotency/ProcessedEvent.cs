namespace OpenTicket.Ddd.Application.IntegrationEvents.Idempotency;

/// <summary>
/// Represents a record of a processed integration event.
/// Used for idempotency checking to prevent duplicate processing.
/// </summary>
public class ProcessedEvent
{
    /// <summary>
    /// The event ID that was processed.
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// The type of event that was processed.
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// The consumer group that processed this event.
    /// </summary>
    public string ConsumerGroup { get; init; } = string.Empty;

    /// <summary>
    /// When the event was processed.
    /// </summary>
    public DateTime ProcessedAt { get; init; }
}
