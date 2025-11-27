using System.Collections.Concurrent;

namespace OpenTicket.Ddd.Application.IntegrationEvents.Outbox;

/// <summary>
/// In-memory implementation of IOutboxRepository.
/// Suitable for testing and MVP mode.
/// </summary>
public sealed class InMemoryOutboxRepository : IOutboxRepository
{
    private readonly ConcurrentDictionary<Guid, OutboxMessage> _messages = new();

    public Task AddAsync(OutboxMessage message, CancellationToken ct = default)
    {
        _messages.TryAdd(message.Id, message);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IEnumerable<OutboxMessage> messages, CancellationToken ct = default)
    {
        foreach (var message in messages)
        {
            _messages.TryAdd(message.Id, message);
        }
        return Task.CompletedTask;
    }

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
            message.Status = OutboxMessageStatus.Processing;
        }

        return Task.FromResult<IReadOnlyList<OutboxMessage>>(pending);
    }

    public Task MarkAsPublishedAsync(Guid id, CancellationToken ct = default)
    {
        if (_messages.TryGetValue(id, out var message))
        {
            message.Status = OutboxMessageStatus.Published;
            message.PublishedAt = DateTime.UtcNow;
        }
        return Task.CompletedTask;
    }

    public Task MarkAsFailedAsync(Guid id, string error, CancellationToken ct = default)
    {
        if (_messages.TryGetValue(id, out var message))
        {
            message.Status = OutboxMessageStatus.Failed;
            message.LastError = error;
        }
        return Task.CompletedTask;
    }

    public Task IncrementRetryAsync(Guid id, string error, CancellationToken ct = default)
    {
        if (_messages.TryGetValue(id, out var message))
        {
            message.RetryCount++;
            message.LastError = error;
            message.Status = OutboxMessageStatus.Pending; // Reset to pending for retry
        }
        return Task.CompletedTask;
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
}
