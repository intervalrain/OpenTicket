namespace OpenTicket.Ddd.Domain;

/// <summary>
/// Base class for aggregate roots. Aggregates are consistency boundaries
/// and the only entry point for modifying a cluster of entities.
/// </summary>
/// <typeparam name="TId">The type of the aggregate root identifier.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

/// <summary>
/// Aggregate root with Guid identifier (default).
/// </summary>
public abstract class AggregateRoot : AggregateRoot<Guid>;
