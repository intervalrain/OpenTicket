namespace OpenTicket.Ddd.Application.IntegrationEvents;

/// <summary>
/// Subscribes to integration events from the message broker.
/// Provides automatic deserialization and handler dispatch with idempotency.
/// </summary>
public interface IIntegrationEventSubscriber
{
    /// <summary>
    /// Subscribes to events of a specific type.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to subscribe to.</typeparam>
    /// <param name="topic">The topic to subscribe to.</param>
    /// <param name="consumerGroup">Consumer group for load balancing.</param>
    /// <param name="ct">Cancellation token to stop subscribing.</param>
    Task SubscribeAsync<TEvent>(
        string topic,
        string consumerGroup,
        CancellationToken ct = default)
        where TEvent : IIntegrationEvent;

    /// <summary>
    /// Subscribes to all integration events on a topic.
    /// Events are dispatched to registered handlers based on their type.
    /// </summary>
    /// <param name="topic">The topic to subscribe to.</param>
    /// <param name="consumerGroup">Consumer group for load balancing.</param>
    /// <param name="ct">Cancellation token to stop subscribing.</param>
    Task SubscribeAllAsync(
        string topic,
        string consumerGroup,
        CancellationToken ct = default);
}
