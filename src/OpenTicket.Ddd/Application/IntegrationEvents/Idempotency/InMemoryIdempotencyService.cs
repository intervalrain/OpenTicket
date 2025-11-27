using System.Collections.Concurrent;

namespace OpenTicket.Ddd.Application.IntegrationEvents.Idempotency;

/// <summary>
/// In-memory implementation of IIdempotencyService.
/// Suitable for testing and MVP mode.
/// </summary>
public sealed class InMemoryIdempotencyService : IIdempotencyService
{
    private readonly ConcurrentDictionary<string, ProcessedEvent> _processedEvents = new();

    public Task<bool> HasBeenProcessedAsync(Guid eventId, string consumerGroup, CancellationToken ct = default)
    {
        var key = GetKey(eventId, consumerGroup);
        return Task.FromResult(_processedEvents.ContainsKey(key));
    }

    public Task MarkAsProcessedAsync(Guid eventId, string eventType, string consumerGroup, CancellationToken ct = default)
    {
        var key = GetKey(eventId, consumerGroup);
        var processedEvent = new ProcessedEvent
        {
            EventId = eventId,
            EventType = eventType,
            ConsumerGroup = consumerGroup,
            ProcessedAt = DateTime.UtcNow
        };

        _processedEvents.TryAdd(key, processedEvent);
        return Task.CompletedTask;
    }

    public Task<int> CleanupAsync(DateTime olderThan, CancellationToken ct = default)
    {
        var toDelete = _processedEvents
            .Where(kvp => kvp.Value.ProcessedAt < olderThan)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toDelete)
        {
            _processedEvents.TryRemove(key, out _);
        }

        return Task.FromResult(toDelete.Count);
    }

    private static string GetKey(Guid eventId, string consumerGroup)
        => $"{consumerGroup}:{eventId}";
}
