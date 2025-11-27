namespace OpenTicket.Infrastructure.MessageBroker.Abstractions;

/// <summary>
/// Represents a message that can be sent through the message broker.
/// </summary>
public interface IMessage
{
    /// <summary>
    /// Unique identifier for this message.
    /// </summary>
    string MessageId { get; }

    /// <summary>
    /// The partition key used to determine which partition this message belongs to.
    /// Messages with the same partition key are guaranteed to be processed in order.
    /// For Kafka: partition key, NATS: subject suffix, Redis: stream key suffix.
    /// </summary>
    string PartitionKey { get; }

    /// <summary>
    /// Correlation ID for tracing requests across services.
    /// Used for request-response patterns and distributed tracing.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Timestamp when the message was created (UTC).
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// Optional headers for message metadata.
    /// Provider-agnostic way to pass additional context.
    /// </summary>
    IDictionary<string, string>? Headers { get; }
}

/// <summary>
/// Base implementation of IMessage.
/// </summary>
public abstract record Message : IMessage
{
    public string MessageId { get; init; } = Guid.NewGuid().ToString();
    public abstract string PartitionKey { get; }
    public string? CorrelationId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public IDictionary<string, string>? Headers { get; init; }
}
