using System.Collections.Concurrent;

namespace OpenTicket.Ddd.Application.IntegrationEvents.Outbox;

/// <summary>
/// In-memory implementation of IOutboxRepository.
/// Suitable for testing and MVP mode.
/// </summary>
public sealed class InMemoryOutboxRepository : IOutboxRepository
{
    private readonly ConcurrentDictionary<Guid, OutboxMessage> _messages = new();

    #region IRepository<OutboxMessage> Implementation

    public Task<OutboxMessage> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (_messages.TryGetValue(id, out var message))
            return Task.FromResult(message);

        throw new KeyNotFoundException($"OutboxMessage with id {id} not found");
    }

    public Task<OutboxMessage?> FindAsync(Guid id, CancellationToken ct = default)
    {
        _messages.TryGetValue(id, out var message);
        return Task.FromResult(message);
    }

    public Task<IReadOnlyList<OutboxMessage>> ListAllAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<OutboxMessage>>(_messages.Values.ToList());
    }

    public Task InsertAsync(OutboxMessage aggregate, CancellationToken ct = default)
    {
        _messages.TryAdd(aggregate.Id, aggregate);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(OutboxMessage aggregate, CancellationToken ct = default)
    {
        _messages[aggregate.Id] = aggregate;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(OutboxMessage aggregate, CancellationToken ct = default)
    {
        _messages.TryRemove(aggregate.Id, out _);
        return Task.CompletedTask;
    }

    #endregion

    #region IOutboxRepository Specific Methods

    public Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct = default)
    {
        var pending = _messages.Values
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToList();

        // Mark as processing to prevent concurrent processing
        foreach (var message in pending)
        {
            message.MarkAsProcessing();
        }

        return Task.FromResult<IReadOnlyList<OutboxMessage>>(pending);
    }

    public Task<int> DeletePublishedAsync(DateTime olderThan, CancellationToken ct = default)
    {
        var toDelete = _messages.Values
            .Where(m => m.Status == OutboxMessageStatus.Published && m.PublishedAt < olderThan)
            .Select(m => m.Id)
            .ToList();

        foreach (var id in toDelete)
        {
            _messages.TryRemove(id, out _);
        }

        return Task.FromResult(toDelete.Count);
    }

    #endregion
}
