using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTicket.Infrastructure.MessageBroker.Abstractions;

namespace OpenTicket.Infrastructure.MessageBroker.InMemory;

/// <summary>
/// In-memory implementation of IMessageBroker for MVP/testing mode.
/// Messages are stored in memory and processed directly without external dependencies.
/// </summary>
public sealed class InMemoryMessageBroker : IMessageBroker
{
    private readonly MessageBrokerOptions _options;
    private readonly ILogger<InMemoryMessageBroker> _logger;
    private readonly ConcurrentDictionary<string, ConcurrentQueue<MessageEnvelope>> _topics = new();
    private readonly ConcurrentDictionary<string, List<Func<MessageEnvelope, CancellationToken, Task>>> _handlers = new();
    private readonly HashSet<string> _createdTopics = new();

    public InMemoryMessageBroker(
        IOptions<MessageBrokerOptions> options,
        ILogger<InMemoryMessageBroker> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public int PartitionCount => _options.PartitionCount;

    public int GetPartition(string partitionKey)
    {
        var hash = partitionKey.GetHashCode();
        return Math.Abs(hash % PartitionCount);
    }

    public Task EnsureTopicExistsAsync(string topic, CancellationToken ct = default)
    {
        _topics.TryAdd(topic, new ConcurrentQueue<MessageEnvelope>());
        _createdTopics.Add(topic);
        _logger.LogDebug("Ensured topic {Topic} exists (in-memory)", topic);
        return Task.CompletedTask;
    }

    public async Task<string> PublishAsync<TMessage>(string topic, TMessage message, CancellationToken ct = default)
        where TMessage : IMessage
    {
        await EnsureTopicExistsAsync(topic, ct);

        var envelope = new MessageEnvelope
        {
            MessageId = message.MessageId,
            TypeName = typeof(TMessage).AssemblyQualifiedName!,
            Payload = JsonSerializer.Serialize(message, message.GetType()),
            PartitionKey = message.PartitionKey,
            CorrelationId = message.CorrelationId,
            CreatedAt = message.CreatedAt,
            Partition = GetPartition(message.PartitionKey)
        };

        // Store in queue
        if (_topics.TryGetValue(topic, out var queue))
        {
            queue.Enqueue(envelope);
        }

        // Dispatch to handlers immediately (in-memory behavior)
        if (_handlers.TryGetValue(topic, out var handlers))
        {
            foreach (var handler in handlers.ToList())
            {
                try
                {
                    await handler(envelope, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in handler for message {MessageId}", message.MessageId);
                }
            }
        }

        _logger.LogDebug(
            "Published message {MessageId} to topic {Topic} (partition {Partition})",
            message.MessageId, topic, envelope.Partition);

        return message.MessageId;
    }

    public async Task<IReadOnlyList<string>> PublishBatchAsync<TMessage>(
        string topic,
        IEnumerable<TMessage> messages,
        CancellationToken ct = default)
        where TMessage : IMessage
    {
        var messageIds = new List<string>();
        foreach (var message in messages)
        {
            var id = await PublishAsync(topic, message, ct);
            messageIds.Add(id);
        }
        return messageIds;
    }

    public Task SubscribeAsync<TMessage>(
        string topic,
        string consumerGroup,
        Func<IMessageContext<TMessage>, CancellationToken, Task> handler,
        CancellationToken ct = default)
        where TMessage : IMessage
    {
        _handlers.AddOrUpdate(
            topic,
            _ => [CreateHandler(topic, handler)],
            (_, list) =>
            {
                list.Add(CreateHandler(topic, handler));
                return list;
            });

        _logger.LogInformation(
            "Subscribed to topic {Topic} with consumer group {ConsumerGroup} (in-memory)",
            topic, consumerGroup);

        return Task.CompletedTask;
    }

    public Task SubscribeToPartitionAsync<TMessage>(
        string topic,
        string consumerGroup,
        int partition,
        Func<IMessageContext<TMessage>, CancellationToken, Task> handler,
        CancellationToken ct = default)
        where TMessage : IMessage
    {
        Func<MessageEnvelope, CancellationToken, Task> wrappedHandler = async (envelope, token) =>
        {
            if (envelope.Partition != partition) return;

            var messageType = Type.GetType(envelope.TypeName);
            if (messageType == null) return;

            var message = (TMessage)JsonSerializer.Deserialize(envelope.Payload, messageType)!;
            var context = CreateMessageContext(message, topic, envelope.Partition);
            await handler(context, token);
        };

        _handlers.AddOrUpdate(
            topic,
            _ => [wrappedHandler],
            (_, list) =>
            {
                list.Add(wrappedHandler);
                return list;
            });

        _logger.LogInformation(
            "Subscribed to partition {Partition} of topic {Topic} with consumer group {ConsumerGroup} (in-memory)",
            partition, topic, consumerGroup);

        return Task.CompletedTask;
    }

    public Task<long> GetPendingCountAsync(string topic, string consumerGroup, CancellationToken ct = default)
    {
        if (_topics.TryGetValue(topic, out var queue))
        {
            return Task.FromResult((long)queue.Count);
        }
        return Task.FromResult(0L);
    }

    public Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }

    private Func<MessageEnvelope, CancellationToken, Task> CreateHandler<TMessage>(
        string topic,
        Func<IMessageContext<TMessage>, CancellationToken, Task> handler)
        where TMessage : IMessage
    {
        return async (envelope, token) =>
        {
            var messageType = Type.GetType(envelope.TypeName);
            if (messageType == null || !typeof(TMessage).IsAssignableFrom(messageType)) return;

            var message = (TMessage)JsonSerializer.Deserialize(envelope.Payload, messageType)!;
            var context = CreateMessageContext(message, topic, envelope.Partition);
            await handler(context, token);
        };
    }

    private static MessageContext<TMessage> CreateMessageContext<TMessage>(
        TMessage message,
        string topic,
        int partition)
        where TMessage : IMessage
    {
        return new MessageContext<TMessage>(
            message,
            topic,
            partition,
            ack: _ => Task.CompletedTask,
            nak: (_, _) => Task.CompletedTask,
            delay: (_, _) => Task.CompletedTask);
    }

    private sealed class MessageEnvelope
    {
        public string MessageId { get; set; } = "";
        public string TypeName { get; set; } = "";
        public string Payload { get; set; } = "";
        public string PartitionKey { get; set; } = "";
        public string? CorrelationId { get; set; }
        public DateTime CreatedAt { get; set; }
        public int Partition { get; set; }
    }
}
