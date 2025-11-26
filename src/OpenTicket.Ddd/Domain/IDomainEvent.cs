namespace OpenTicket.Ddd.Domain;

/// <summary>
/// Marker interface for domain events.
/// Domain events represent something that happened in the domain.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// The unique identifier for this event instance.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// When the event occurred.
    /// </summary>
    DateTime OccurredAt { get; }
}