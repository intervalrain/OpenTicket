namespace OpenTicket.Infrastructure.MessageBroker.Abstractions;

/// <summary>
/// Unified interface for message broker operations.
/// Combines producer and consumer capabilities with administrative functions.
/// </summary>
public interface IMessageBroker : IMessageProducer, IMessageConsumer
{
    /// <summary>
    /// Gets the total number of partitions configured for this broker.
    /// </summary>
    int PartitionCount { get; }

    /// <summary>
    /// Calculates the partition number for a given partition key.
    /// Uses consistent hashing to ensure same key always maps to same partition.
    /// </summary>
    /// <param name="partitionKey">The partition key.</param>
    /// <returns>The partition number (0 to PartitionCount-1).</returns>
    int GetPartition(string partitionKey);

    /// <summary>
    /// Ensures the topic exists with the required configuration.
    /// Creates the topic/stream/queue if it doesn't exist.
    /// </summary>
    /// <param name="topic">The topic name.</param>
    /// <param name="ct">Cancellation token.</param>
    Task EnsureTopicExistsAsync(string topic, CancellationToken ct = default);

    /// <summary>
    /// Gets the approximate number of messages pending for a consumer group.
    /// </summary>
    /// <param name="topic">The topic name.</param>
    /// <param name="consumerGroup">The consumer group name.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<long> GetPendingCountAsync(string topic, string consumerGroup, CancellationToken ct = default);

    /// <summary>
    /// Checks if the broker connection is healthy.
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}
