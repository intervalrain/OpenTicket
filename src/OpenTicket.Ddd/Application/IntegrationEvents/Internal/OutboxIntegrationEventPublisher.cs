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
        var message = CreateOutboxMessage(@event);
        await _outboxRepository.AddAsync(message, ct);
    }

    public async Task PublishAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken ct = default)
        where TEvent : IIntegrationEvent
    {
        var messages = events.Select(CreateOutboxMessage).ToList();
        await _outboxRepository.AddRangeAsync(messages, ct);
    }

    private static OutboxMessage CreateOutboxMessage<TEvent>(TEvent @event)
        where TEvent : IIntegrationEvent
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = @event.EventId,
            EventType = @event.EventType,
            AggregateId = @event.AggregateId,
            Payload = IntegrationEventSerializer.Serialize(@event),
            CorrelationId = @event.CorrelationId,
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending
        };
    }
}
