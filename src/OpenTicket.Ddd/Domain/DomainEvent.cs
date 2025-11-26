namespace OpenTicket.Ddd.Domain;

/// <summary>
/// Base record for domain events.
/// Domain events represent something that happened in the domain.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}