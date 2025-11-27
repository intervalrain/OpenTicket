namespace OpenTicket.Ddd.Application.IntegrationEvents;

/// <summary>
/// Handles integration events received from the message broker.
/// </summary>
/// <typeparam name="TEvent">The type of integration event to handle.</typeparam>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    /// <summary>
    /// Handles the integration event.
    /// </summary>
    /// <param name="event">The event to handle.</param>
    /// <param name="ct">Cancellation token.</param>
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}
