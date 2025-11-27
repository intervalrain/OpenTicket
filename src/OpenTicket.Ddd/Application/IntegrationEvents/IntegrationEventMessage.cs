namespace OpenTicket.Ddd.Application.IntegrationEvents;

/// <summary>
/// Wrapper for integration events to be sent through the message broker.
/// Implements the message contract required by the broker abstraction.
/// </summary>
public record IntegrationEventMessage
{
    /// <summary>
    /// Unique message identifier.
    /// </summary>
    public string MessageId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The event ID from the integration event.
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// The type name of the event for deserialization.
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// The aggregate ID used for partition routing.
    /// </summary>
    public string AggregateId { get; init; } = string.Empty;

    /// <summary>
    /// Serialized event payload (JSON).
    /// </summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// When the event occurred.
    /// </summary>
    public DateTime OccurredAt { get; init; }

    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
