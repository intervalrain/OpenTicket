using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using OpenTicket.Infrastructure.MessageBroker.Abstractions;

namespace OpenTicket.Infrastructure.MessageBroker.Nats;

/// <summary>
/// NATS JetStream implementation of IMessageBroker.
/// Uses subjects with partitioning for distributed message processing.
/// </summary>
public sealed class NatsMessageBroker : IMessageBroker, IAsyncDisposable
{
    private readonly MessageBrokerOptions _options;
    private readonly NatsOptions _natsOptions;
    private readonly ILogger<NatsMessageBroker> _logger;
    private readonly Lazy<Task<NatsConnection>> _connection;
    private readonly Lazy<Task<NatsJSContext>> _jetStream;
    private bool _disposed;

    public NatsMessageBroker(
        IOptions<MessageBrokerOptions> options,
        IOptions<NatsOptions> natsOptions,
        ILogger<NatsMessageBroker> logger)
    {
        _options = options.Value;
        _natsOptions = natsOptions.Value;
        _logger = logger;

        _connection = new Lazy<Task<NatsConnection>>(async () =>
        {
            var opts = new NatsOpts { Url = _natsOptions.Url };
            var connection = new NatsConnection(opts);
            await connection.ConnectAsync();
            return connection;
        });

        _jetStream = new Lazy<Task<NatsJSContext>>(async () =>
        {
            var conn = await _connection.Value;
            return new NatsJSContext(conn);
        });
    }

    public int PartitionCount => _options.PartitionCount;

    public int GetPartition(string partitionKey)
    {
        var hash = partitionKey.GetHashCode();
        return Math.Abs(hash % PartitionCount);
    }

    private string GetStreamName(string topic) => $"{_natsOptions.StreamPrefix}_{topic}".ToUpperInvariant();
    private string GetSubject(string topic, int partition) => $"{_natsOptions.StreamPrefix}.{topic}.{partition}";
    private string GetSubjectWildcard(string topic) => $"{_natsOptions.StreamPrefix}.{topic}.*";

    public async Task EnsureTopicExistsAsync(string topic, CancellationToken ct = default)
    {
        var js = await _jetStream.Value;
        var streamName = GetStreamName(topic);
        var subjectWildcard = GetSubjectWildcard(topic);

        try
        {
            await js.GetStreamAsync(streamName, cancellationToken: ct);
            _logger.LogDebug("Stream {StreamName} already exists", streamName);
        }
        catch (NatsJSApiException ex) when (ex.Error.Code == 404)
        {
            var config = new StreamConfig(streamName, [subjectWildcard])
            {
                MaxAge = _natsOptions.MaxAge,
                Storage = StreamConfigStorage.File,
                Retention = StreamConfigRetention.Interest,
                Discard = StreamConfigDiscard.Old,
                DuplicateWindow = _options.EnableDeduplication ? _options.DeduplicationWindow : TimeSpan.Zero,
                NumReplicas = 1
            };

            await js.CreateStreamAsync(config, ct);
            _logger.LogInformation("Created stream {StreamName} with subject {Subject}", streamName, subjectWildcard);
        }
    }

    public async Task<string> PublishAsync<TMessage>(string topic, TMessage message, CancellationToken ct = default)
        where TMessage : IMessage
    {
        var js = await _jetStream.Value;
        var partition = GetPartition(message.PartitionKey);
        var subject = GetSubject(topic, partition);

        var envelope = new MessageEnvelope
        {
            MessageId = message.MessageId,
            TypeName = typeof(TMessage).AssemblyQualifiedName!,
            Payload = JsonSerializer.Serialize(message, message.GetType()),
            PartitionKey = message.PartitionKey,
            CorrelationId = message.CorrelationId,
            CreatedAt = message.CreatedAt
        };

        var data = JsonSerializer.SerializeToUtf8Bytes(envelope);
        var headers = new NatsHeaders();

        if (message.Headers != null)
        {
            foreach (var header in message.Headers)
            {
                headers.Add(header.Key, header.Value);
            }
        }

        var ack = await js.PublishAsync(
            subject,
            data,
            opts: new NatsJSPubOpts { MsgId = message.MessageId },
            headers: headers,
            cancellationToken: ct);

        ack.EnsureSuccess();

        _logger.LogDebug(
            "Published message {MessageId} to {Subject} (seq: {Sequence})",
            message.MessageId, subject, ack.Seq);

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

    public async Task SubscribeAsync<TMessage>(
        string topic,
        string consumerGroup,
        Func<IMessageContext<TMessage>, CancellationToken, Task> handler,
        CancellationToken ct = default)
        where TMessage : IMessage
    {
        await EnsureTopicExistsAsync(topic, ct);

        var js = await _jetStream.Value;
        var streamName = GetStreamName(topic);
        var consumerName = $"{consumerGroup}-{Environment.MachineName}";

        // Create or get durable consumer
        INatsJSConsumer consumer;
        try
        {
            consumer = await js.GetConsumerAsync(streamName, consumerName, ct);
        }
        catch (NatsJSApiException ex) when (ex.Error.Code == 404)
        {
            var config = new ConsumerConfig(consumerName)
            {
                DurableName = consumerName,
                DeliverPolicy = ConsumerConfigDeliverPolicy.All,
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
                AckWait = _natsOptions.AckWait,
                MaxDeliver = _natsOptions.MaxDeliver,
                FilterSubject = GetSubjectWildcard(topic)
            };

            consumer = await js.CreateConsumerAsync(streamName, config, ct);
        }

        _logger.LogInformation(
            "Started consuming from stream {StreamName} as {ConsumerName}",
            streamName, consumerName);

        try
        {
            await foreach (var msg in consumer.ConsumeAsync<byte[]>(cancellationToken: ct))
            {
                await ProcessNatsMessageAsync(msg, topic, handler, ct);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Stopped consuming from stream {StreamName}", streamName);
        }
    }

    public async Task SubscribeToPartitionAsync<TMessage>(
        string topic,
        string consumerGroup,
        int partition,
        Func<IMessageContext<TMessage>, CancellationToken, Task> handler,
        CancellationToken ct = default)
        where TMessage : IMessage
    {
        await EnsureTopicExistsAsync(topic, ct);

        var js = await _jetStream.Value;
        var streamName = GetStreamName(topic);
        var subject = GetSubject(topic, partition);
        var consumerName = $"{consumerGroup}-p{partition}-{Environment.MachineName}";

        INatsJSConsumer consumer;
        try
        {
            consumer = await js.GetConsumerAsync(streamName, consumerName, ct);
        }
        catch (NatsJSApiException ex) when (ex.Error.Code == 404)
        {
            var config = new ConsumerConfig(consumerName)
            {
                DurableName = consumerName,
                DeliverPolicy = ConsumerConfigDeliverPolicy.All,
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
                AckWait = _natsOptions.AckWait,
                MaxDeliver = _natsOptions.MaxDeliver,
                FilterSubject = subject
            };

            consumer = await js.CreateConsumerAsync(streamName, config, ct);
        }

        _logger.LogInformation(
            "Started consuming partition {Partition} from stream {StreamName} as {ConsumerName}",
            partition, streamName, consumerName);

        try
        {
            await foreach (var msg in consumer.ConsumeAsync<byte[]>(cancellationToken: ct))
            {
                await ProcessNatsMessageAsync(msg, topic, handler, ct);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Stopped consuming partition {Partition} from stream {StreamName}", partition, streamName);
        }
    }

    private async Task ProcessNatsMessageAsync<TMessage>(
        NatsJSMsg<byte[]> msg,
        string topic,
        Func<IMessageContext<TMessage>, CancellationToken, Task> handler,
        CancellationToken ct)
        where TMessage : IMessage
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<MessageEnvelope>(msg.Data);
            if (envelope == null)
            {
                await msg.AckAsync(cancellationToken: ct);
                return;
            }

            var messageType = Type.GetType(envelope.TypeName);
            if (messageType == null)
            {
                _logger.LogWarning("Unknown message type: {TypeName}", envelope.TypeName);
                await msg.AckAsync(cancellationToken: ct);
                return;
            }

            var message = (TMessage)JsonSerializer.Deserialize(envelope.Payload, messageType)!;
            var partition = int.Parse(msg.Subject.Split('.').Last());

            var context = new MessageContext<TMessage>(
                message,
                topic,
                partition,
                ack: async _ => await msg.AckAsync(cancellationToken: ct),
                nak: async (requeue, _) =>
                {
                    if (requeue)
                        await msg.NakAsync(cancellationToken: ct);
                    else
                        await msg.AckTerminateAsync(cancellationToken: ct);
                },
                delay: async (_, ct2) =>
                {
                    // NATS JetStream doesn't support delay natively, fall back to nak
                    await msg.NakAsync(cancellationToken: ct2);
                });

            await handler(context, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error processing message from {Subject}", msg.Subject);
            await msg.NakAsync(cancellationToken: ct);
        }
    }

    public async Task<long> GetPendingCountAsync(string topic, string consumerGroup, CancellationToken ct = default)
    {
        var js = await _jetStream.Value;
        var streamName = GetStreamName(topic);

        try
        {
            var stream = await js.GetStreamAsync(streamName, cancellationToken: ct);
            return (long)stream.Info.State.Messages;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var conn = await _connection.Value;
            return conn.ConnectionState == NatsConnectionState.Open;
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        if (_connection.IsValueCreated)
        {
            var conn = await _connection.Value;
            await conn.DisposeAsync();
        }

        _disposed = true;
    }

    private sealed class MessageEnvelope
    {
        public string MessageId { get; set; } = "";
        public string TypeName { get; set; } = "";
        public string Payload { get; set; } = "";
        public string PartitionKey { get; set; } = "";
        public string? CorrelationId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
