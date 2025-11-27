namespace OpenTicket.Infrastructure.MessageBroker.Abstractions;

/// <summary>
/// Consumes messages from a message broker.
/// Provides a unified interface across different message broker implementations.
/// </summary>
public interface IMessageConsumer
{
    /// <summary>
    /// Subscribes to a topic and processes messages as they arrive.
    /// The handler receives a context that allows for ack/nak operations.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to consume.</typeparam>
    /// <param name="topic">The topic/queue name to subscribe to.</param>
    /// <param name="consumerGroup">Consumer group for load balancing across instances.</param>
    /// <param name="handler">The handler to process each message with its context.</param>
    /// <param name="ct">Cancellation token to stop consuming.</param>
    Task SubscribeAsync<TMessage>(
        string topic,
        string consumerGroup,
        Func<IMessageContext<TMessage>, CancellationToken, Task> handler,
        CancellationToken ct = default)
        where TMessage : IMessage;

    /// <summary>
    /// Subscribes to a specific partition of a topic.
    /// Use for scenarios requiring explicit partition assignment.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to consume.</typeparam>
    /// <param name="topic">The topic/queue name to subscribe to.</param>
    /// <param name="consumerGroup">Consumer group for tracking offsets.</param>
    /// <param name="partition">The specific partition to consume from.</param>
    /// <param name="handler">The handler to process each message with its context.</param>
    /// <param name="ct">Cancellation token to stop consuming.</param>
    Task SubscribeToPartitionAsync<TMessage>(
        string topic,
        string consumerGroup,
        int partition,
        Func<IMessageContext<TMessage>, CancellationToken, Task> handler,
        CancellationToken ct = default)
        where TMessage : IMessage;
}
