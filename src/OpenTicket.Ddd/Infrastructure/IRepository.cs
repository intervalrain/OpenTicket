using OpenTicket.Ddd.Domain;

namespace OpenTicket.Ddd.Infrastructure;

/// <summary>
/// Base interface for repositories. Repositories provide access to aggregates.
/// </summary>
/// <typeparam name="TAggregate">The aggregate root type.</typeparam>
/// <typeparam name="TId">The identifier type.</typeparam>
public interface IRepository<TAggregate, in TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    /// <summary>
    /// Gets an aggregate by its identifier. Throws if not found.
    /// </summary>
    Task<TAggregate> GetAsync(TId id, CancellationToken ct = default);

    /// <summary>
    /// Finds an aggregate by its identifier, returns null if not found.
    /// </summary>
    Task<TAggregate?> FindAsync(TId id, CancellationToken ct = default);

    /// <summary>
    /// Lists all aggregates. Use sparingly, prefer specific query repositories for complex queries.
    /// </summary>
    Task<IReadOnlyList<TAggregate>> ListAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Inserts a new aggregate.
    /// </summary>
    Task InsertAsync(TAggregate aggregate, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing aggregate.
    /// </summary>
    Task UpdateAsync(TAggregate aggregate, CancellationToken ct = default);

    /// <summary>
    /// Deletes an aggregate.
    /// </summary>
    Task DeleteAsync(TAggregate aggregate, CancellationToken ct = default);
}

/// <summary>
/// Repository for aggregates with Guid identifier (default).
/// </summary>
/// <typeparam name="TAggregate">The aggregate root type.</typeparam>
public interface IRepository<TAggregate> : IRepository<TAggregate, Guid>
    where TAggregate : AggregateRoot<Guid>;
