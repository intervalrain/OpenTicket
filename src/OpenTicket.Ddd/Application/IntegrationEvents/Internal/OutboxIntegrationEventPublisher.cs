using OpenTicket.Ddd.Application.IntegrationEvents.Outbox;

namespace OpenTicket.Ddd.Application.IntegrationEvents.Internal;

/// <summary>
/// Outbox-based implementation of IIntegrationEventPublisher.
/// Events are stored in the outbox for reliable delivery.
/// </summary>
public sealed class OutboxIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly IOutboxRepository _outboxRepository;

    public OutboxIntegrationEventPublisher(IOutboxRepository outboxRepository)
    {
        _outboxRepository = outboxRepository;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IIntegrationEvent
    {
        var message = OutboxMessage.Create(
            @event.EventId,
            @event.EventType,
            @event.AggregateId,
            IntegrationEventSerializer.Serialize(@event),
            @event.CorrelationId);

        await _outboxRepository.InsertAsync(message, ct);
    }

    public async Task PublishAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken ct = default)
        where TEvent : IIntegrationEvent
    {
        foreach (var @event in events)
        {
            await PublishAsync(@event, ct);
        }
    }
}
