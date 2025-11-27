namespace OpenTicket.Ddd.Application.IntegrationEvents;

/// <summary>
/// Marker interface for integration events.
/// Integration events are used for inter-service communication via message broker.
/// Unlike domain events (in-process), integration events cross service boundaries.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// Used for idempotency checking.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// When the event occurred (UTC).
    /// </summary>
    DateTime OccurredAt { get; }

    /// <summary>
    /// The type name of the event for deserialization.
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// The aggregate ID that this event relates to.
    /// Used as partition key for ordered processing.
    /// </summary>
    string AggregateId { get; }
}

/// <summary>
/// Base implementation of IIntegrationEvent.
/// </summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public virtual string EventType => GetType().Name;
    public string? CorrelationId { get; init; }
    public abstract string AggregateId { get; }
}
