using System.Collections.Concurrent;
using OpenTicket.Ddd.Application;
using OpenTicket.Ddd.Domain;

namespace OpenTicket.Infrastructure.Database.InMemory;

/// <summary>
/// In-memory implementation of IRepository for MVP mode.
/// </summary>
/// <typeparam name="TAggregate">The aggregate root type.</typeparam>
public sealed class InMemoryRepository<TAggregate> : IRepository<TAggregate>
    where TAggregate : AggregateRoot<Guid>
{
    private readonly ConcurrentDictionary<Guid, TAggregate> _entities = new();

    public Task<TAggregate> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (!_entities.TryGetValue(id, out var entity))
            throw new KeyNotFoundException($"Entity with id {id} not found");

        return Task.FromResult(entity);
    }

    public Task<TAggregate?> FindAsync(Guid id, CancellationToken ct = default)
    {
        _entities.TryGetValue(id, out var entity);
        return Task.FromResult(entity);
    }

    public Task<IReadOnlyList<TAggregate>> ListAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<TAggregate> entities = _entities.Values.ToList();
        return Task.FromResult(entities);
    }

    public Task InsertAsync(TAggregate aggregate, CancellationToken ct = default)
    {
        _entities.TryAdd(aggregate.Id, aggregate);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TAggregate aggregate, CancellationToken ct = default)
    {
        _entities[aggregate.Id] = aggregate;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(TAggregate aggregate, CancellationToken ct = default)
    {
        _entities.TryRemove(aggregate.Id, out _);
        return Task.CompletedTask;
    }
}
