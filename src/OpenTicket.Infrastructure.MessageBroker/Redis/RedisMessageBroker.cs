using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTicket.Infrastructure.MessageBroker.Abstractions;
using StackExchange.Redis;

namespace OpenTicket.Infrastructure.MessageBroker.Redis;

/// <summary>
/// Redis Streams implementation of IMessageBroker.
/// Uses Consumer Groups for distributed message processing with partition support.
/// </summary>
public sealed class RedisMessageBroker : IMessageBroker, IAsyncDisposable
{
    private readonly MessageBrokerOptions _options;
    private readonly RedisOptions _redisOptions;
    private readonly ILogger<RedisMessageBroker> _logger;
    private readonly Lazy<ConnectionMultiplexer> _connection;
    private bool _disposed;

    public RedisMessageBroker(
        IOptions<MessageBrokerOptions> options,
        IOptions<RedisOptions> redisOptions,
        ILogger<RedisMessageBroker> logger)
    {
        _options = options.Value;
        _redisOptions = redisOptions.Value;
        _logger = logger;
        _connection = new Lazy<ConnectionMultiplexer>(() =>
            ConnectionMultiplexer.Connect(_redisOptions.ConnectionString));
    }

    private IDatabase Database => _connection.Value.GetDatabase();

    public int PartitionCount => _options.PartitionCount;

    public int GetPartition(string partitionKey)
    {
        var hash = partitionKey.GetHashCode();
        return Math.Abs(hash % PartitionCount);
    }

    private string GetStreamKey(string topic, int partition) =>
        $"{_redisOptions.KeyPrefix}:{topic}:p{partition}";

    public async Task EnsureTopicExistsAsync(string topic, CancellationToken ct = default)
    {
        for (var partition = 0; partition < PartitionCount; partition++)
        {
            var streamKey = GetStreamKey(topic, partition);
            // Stream is auto-created on first write, but we ensure it exists for consumer groups
            try
            {
                await Database.StreamCreateConsumerGroupAsync(
                    streamKey,
                    $"default-{topic}",
                    StreamPosition.NewMessages,
                    createStream: true);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
            {
                // Consumer group already exists
            }
        }

        _logger.LogDebug("Ensured topic {Topic} exists with {PartitionCount} partitions", topic, PartitionCount);
    }

    public async Task<string> PublishAsync<TMessage>(string topic, TMessage message, CancellationToken ct = default)
        where TMessage : IMessage
    {
        var partition = GetPartition(message.PartitionKey);
        var streamKey = GetStreamKey(topic, partition);

        var serialized = JsonSerializer.Serialize(message, message.GetType());
        var entries = new NameValueEntry[]
        {
            new("id", message.MessageId),
            new("type", typeof(TMessage).AssemblyQualifiedName!),
            new("payload", serialized),
            new("partitionKey", message.PartitionKey),
            new("correlationId", message.CorrelationId ?? ""),
            new("createdAt", message.CreatedAt.ToString("O"))
        };

        var entryId = await Database.StreamAddAsync(
            streamKey,
            entries,
            maxLength: _redisOptions.MaxStreamLength > 0 ? _redisOptions.MaxStreamLength : null,
            useApproximateMaxLength: true);

        _logger.LogDebug(
            "Published message {MessageId} to {StreamKey} with entry {EntryId}",
            message.MessageId, streamKey, entryId);

        return message.MessageId;
    }

    public async Task<IReadOnlyList<string>> PublishBatchAsync<TMessage>(
        string topic,
        IEnumerable<TMessage> messages,
        CancellationToken ct = default)
        where TMessage : IMessage
    {
        var messageIds = new List<string>();
        var messagesByPartition = messages.GroupBy(m => GetPartition(m.PartitionKey));

        foreach (var group in messagesByPartition)
        {
            var partition = group.Key;
            var streamKey = GetStreamKey(topic, partition);
            var batch = Database.CreateBatch();
            var tasks = new List<Task<RedisValue>>();

            foreach (var message in group)
            {
                var serialized = JsonSerializer.Serialize(message, message.GetType());
                var entries = new NameValueEntry[]
                {
                    new("id", message.MessageId),
                    new("type", typeof(TMessage).AssemblyQualifiedName!),
                    new("payload", serialized),
                    new("partitionKey", message.PartitionKey),
                    new("correlationId", message.CorrelationId ?? ""),
                    new("createdAt", message.CreatedAt.ToString("O"))
                };

                tasks.Add(batch.StreamAddAsync(
                    streamKey,
                    entries,
                    maxLength: _redisOptions.MaxStreamLength > 0 ? _redisOptions.MaxStreamLength : null,
                    useApproximateMaxLength: true));

                messageIds.Add(message.MessageId);
            }

            batch.Execute();
            await Task.WhenAll(tasks);
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
        // Subscribe to all partitions with automatic distribution
        var tasks = new List<Task>();
        for (var partition = 0; partition < PartitionCount; partition++)
        {
            var p = partition; // Capture for closure
            tasks.Add(SubscribeToPartitionAsync(topic, consumerGroup, p, handler, ct));
        }

        await Task.WhenAll(tasks);
    }

    public async Task SubscribeToPartitionAsync<TMessage>(
        string topic,
        string consumerGroup,
        int partition,
        Func<IMessageContext<TMessage>, CancellationToken, Task> handler,
        CancellationToken ct = default)
        where TMessage : IMessage
    {
        var streamKey = GetStreamKey(topic, partition);
        var consumerName = $"{Environment.MachineName}-{partition}-{Guid.NewGuid():N}";

        // Ensure consumer group exists
        try
        {
            await Database.StreamCreateConsumerGroupAsync(
                streamKey,
                consumerGroup,
                StreamPosition.NewMessages,
                createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Already exists
        }

        _logger.LogInformation(
            "Started consuming from {StreamKey} as {ConsumerName} in group {ConsumerGroup}",
            streamKey, consumerName, consumerGroup);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // First, try to claim pending messages from dead consumers
                await ClaimPendingMessagesAsync<TMessage>(streamKey, consumerGroup, consumerName, handler, ct);

                // Then read new messages
                var entries = await Database.StreamReadGroupAsync(
                    streamKey,
                    consumerGroup,
                    consumerName,
                    ">", // New messages only
                    count: _options.BatchSize);

                if (entries.Length == 0)
                {
                    await Task.Delay(_options.PollTimeout, ct);
                    continue;
                }

                foreach (var entry in entries)
                {
                    await ProcessEntryAsync(entry, streamKey, consumerGroup, partition, handler, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Stopped consuming from {StreamKey}", streamKey);
        }
    }

    private async Task ClaimPendingMessagesAsync<TMessage>(
        string streamKey,
        string consumerGroup,
        string consumerName,
        Func<IMessageContext<TMessage>, CancellationToken, Task> handler,
        CancellationToken ct)
        where TMessage : IMessage
    {
        try
        {
            var pending = await Database.StreamPendingMessagesAsync(
                streamKey,
                consumerGroup,
                count: _options.BatchSize,
                consumerName: RedisValue.Null,
                minId: "-",
                maxId: "+");

            foreach (var info in pending)
            {
                if (info.IdleTimeInMilliseconds > _redisOptions.ClaimTimeout.TotalMilliseconds)
                {
                    var claimed = await Database.StreamClaimAsync(
                        streamKey,
                        consumerGroup,
                        consumerName,
                        (long)_redisOptions.ClaimTimeout.TotalMilliseconds,
                        new[] { info.MessageId });

                    foreach (var entry in claimed)
                    {
                        var partition = int.Parse(streamKey.Split(":p")[1]);
                        await ProcessEntryAsync(entry, streamKey, consumerGroup, partition, handler, ct);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error claiming pending messages from {StreamKey}", streamKey);
        }
    }

    private async Task ProcessEntryAsync<TMessage>(
        StreamEntry entry,
        string streamKey,
        string consumerGroup,
        int partition,
        Func<IMessageContext<TMessage>, CancellationToken, Task> handler,
        CancellationToken ct)
        where TMessage : IMessage
    {
        var entryId = entry.Id;
        try
        {
            var typeName = entry["type"].ToString();
            var payload = entry["payload"].ToString();
            var topic = streamKey.Split(':')[1];

            var messageType = Type.GetType(typeName);
            if (messageType == null)
            {
                _logger.LogWarning("Unknown message type: {TypeName}", typeName);
                await Database.StreamAcknowledgeAsync(streamKey, consumerGroup, entryId);
                return;
            }

            var message = (TMessage)JsonSerializer.Deserialize(payload, messageType)!;

            var context = new MessageContext<TMessage>(
                message,
                topic,
                partition,
                ack: async _ => await Database.StreamAcknowledgeAsync(streamKey, consumerGroup, entryId),
                nak: async (requeue, _) =>
                {
                    if (!requeue)
                    {
                        // Move to dead letter or just acknowledge to remove
                        await Database.StreamAcknowledgeAsync(streamKey, consumerGroup, entryId);
                    }
                    // If requeue, message stays in pending list for retry
                });

            await handler(context, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error processing entry {EntryId} from {StreamKey}", entryId, streamKey);
            // Message stays in pending list for retry
        }
    }

    public async Task<long> GetPendingCountAsync(string topic, string consumerGroup, CancellationToken ct = default)
    {
        long total = 0;
        for (var partition = 0; partition < PartitionCount; partition++)
        {
            var streamKey = GetStreamKey(topic, partition);
            try
            {
                var pending = await Database.StreamPendingAsync(streamKey, consumerGroup);
                total += pending.PendingMessageCount;
            }
            catch
            {
                // Stream or group doesn't exist
            }
        }
        return total;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            await Database.PingAsync();
            return true;
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
            await _connection.Value.CloseAsync();
            _connection.Value.Dispose();
        }

        _disposed = true;
    }
}
