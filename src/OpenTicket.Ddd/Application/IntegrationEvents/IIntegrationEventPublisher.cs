namespace OpenTicket.Ddd.Application.IntegrationEvents;

/// <summary>
/// Publishes integration events to the message broker.
/// Implementations should use the Outbox pattern for reliable delivery.
/// </summary>
public interface IIntegrationEventPublisher
{
    /// <summary>
    /// Publishes an integration event.
    /// The event is first stored in the outbox, then published to the message broker.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to publish.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IIntegrationEvent;

    /// <summary>
    /// Publishes multiple integration events.
    /// Events are stored in the outbox as a batch, then published.
    /// </summary>
    /// <typeparam name="TEvent">The type of events to publish.</typeparam>
    /// <param name="events">The events to publish.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PublishAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken ct = default)
        where TEvent : IIntegrationEvent;
}
