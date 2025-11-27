namespace OpenTicket.Infrastructure.MessageBroker.Abstractions;

/// <summary>
/// Produces messages to a message broker.
/// Provides a unified interface across different message broker implementations.
/// </summary>
public interface IMessageProducer
{
    /// <summary>
    /// Publishes a message to the specified topic.
    /// The message will be routed to the appropriate partition based on its PartitionKey.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to publish.</typeparam>
    /// <param name="topic">The topic/queue name to publish to.</param>
    /// <param name="message">The message to publish.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The published message ID for tracking.</returns>
    Task<string> PublishAsync<TMessage>(string topic, TMessage message, CancellationToken ct = default)
        where TMessage : IMessage;

    /// <summary>
    /// Publishes multiple messages to the specified topic in a batch.
    /// Messages are grouped by partition key for optimal performance.
    /// </summary>
    /// <typeparam name="TMessage">The type of messages to publish.</typeparam>
    /// <param name="topic">The topic/queue name to publish to.</param>
    /// <param name="messages">The messages to publish.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The published message IDs for tracking.</returns>
    Task<IReadOnlyList<string>> PublishBatchAsync<TMessage>(string topic, IEnumerable<TMessage> messages, CancellationToken ct = default)
        where TMessage : IMessage;
}
