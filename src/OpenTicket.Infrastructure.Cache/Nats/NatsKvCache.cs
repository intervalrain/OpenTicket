using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;
using OpenTicket.Infrastructure.Cache.Abstractions;

namespace OpenTicket.Infrastructure.Cache.Nats;

/// <summary>
/// NATS KV Store implementation of IDistributedCache.
/// Good option for environments already using NATS JetStream.
/// </summary>
public sealed class NatsKvCache : IDistributedCache, IAsyncDisposable
{
    private readonly CacheOptions _cacheOptions;
    private readonly NatsKvCacheOptions _natsOptions;
    private readonly ILogger<NatsKvCache> _logger;
    private readonly Lazy<Task<(NatsConnection, INatsKVStore)>> _connectionTask;
    private bool _disposed;

    public NatsKvCache(
        IOptions<CacheOptions> cacheOptions,
        IOptions<NatsKvCacheOptions> natsOptions,
        ILogger<NatsKvCache> logger)
    {
        _cacheOptions = cacheOptions.Value;
        _natsOptions = natsOptions.Value;
        _logger = logger;
        _connectionTask = new Lazy<Task<(NatsConnection, INatsKVStore)>>(ConnectAsync);
    }

    private async Task<(NatsConnection, INatsKVStore)> ConnectAsync()
    {
        _logger.LogInformation("Connecting to NATS KV at {Url}", _natsOptions.Url);

        var opts = new NatsOpts
        {
            Url = _natsOptions.Url,
            ConnectTimeout = _natsOptions.ConnectTimeout
        };

        var connection = new NatsConnection(opts);
        await connection.ConnectAsync();

        // Create JetStream context first, then KV context
        var jsContext = new NatsJSContext(connection);
        var kvContext = new NatsKVContext(jsContext);

        // Create or get the KV bucket
        var store = await kvContext.CreateStoreAsync(new NatsKVConfig(_natsOptions.BucketName)
        {
            History = _natsOptions.MaxHistory
        });

        _logger.LogInformation("Connected to NATS KV bucket {BucketName}", _natsOptions.BucketName);

        return (connection, store);
    }

    private async Task<INatsKVStore> GetStoreAsync()
    {
        var (_, store) = await _connectionTask.Value;
        return store;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var prefixedKey = GetPrefixedKey(key);
            var store = await GetStoreAsync();
            var entry = await store.GetEntryAsync<string>(prefixedKey, cancellationToken: ct);

            if (entry.Value == null)
                return default;

            return JsonSerializer.Deserialize<T>(entry.Value);
        }
        catch (NatsKVKeyNotFoundException)
        {
            return default;
        }
        catch (NatsKVKeyDeletedException)
        {
            return default;
        }
    }

    public async Task<string?> GetStringAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var prefixedKey = GetPrefixedKey(key);
            var store = await GetStoreAsync();
            var entry = await store.GetEntryAsync<string>(prefixedKey, cancellationToken: ct);

            return entry.Value;
        }
        catch (NatsKVKeyNotFoundException)
        {
            return null;
        }
        catch (NatsKVKeyDeletedException)
        {
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var prefixedKey = GetPrefixedKey(key);
        var serialized = JsonSerializer.Serialize(value);

        var store = await GetStoreAsync();
        await store.PutAsync(prefixedKey, serialized, cancellationToken: ct);

        // Note: NATS KV doesn't support per-key TTL directly
        // TTL is bucket-level. For per-key TTL, we'd need a cleanup mechanism.
        // For now, we rely on bucket-level TTL or manual cleanup.
    }

    public async Task SetStringAsync(string key, string value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var prefixedKey = GetPrefixedKey(key);

        var store = await GetStoreAsync();
        await store.PutAsync(prefixedKey, value, cancellationToken: ct);
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var prefixedKey = GetPrefixedKey(key);
            var store = await GetStoreAsync();
            await store.DeleteAsync(prefixedKey, cancellationToken: ct);
            return true;
        }
        catch (NatsKVKeyNotFoundException)
        {
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var prefixedKey = GetPrefixedKey(key);
            var store = await GetStoreAsync();
            var entry = await store.GetEntryAsync<string>(prefixedKey, cancellationToken: ct);
            return entry.Value != null;
        }
        catch (NatsKVKeyNotFoundException)
        {
            return false;
        }
        catch (NatsKVKeyDeletedException)
        {
            return false;
        }
    }

    public async Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        var prefixedKey = GetPrefixedKey(key);
        var serialized = JsonSerializer.Serialize(value);

        var store = await GetStoreAsync();

        try
        {
            // Create will fail if key already exists
            await store.CreateAsync(prefixedKey, serialized, cancellationToken: ct);
            return true;
        }
        catch (NatsKVCreateException)
        {
            // Key already exists
            return false;
        }
    }

    public async Task<bool> RefreshAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        // NATS KV doesn't support TTL refresh without updating the value
        // We need to read and re-write to "touch" the entry
        try
        {
            var prefixedKey = GetPrefixedKey(key);
            var store = await GetStoreAsync();
            var entry = await store.GetEntryAsync<string>(prefixedKey, cancellationToken: ct);

            if (entry.Value == null)
                return false;

            // Re-put to update timestamp
            await store.PutAsync(prefixedKey, entry.Value, cancellationToken: ct);
            return true;
        }
        catch (NatsKVKeyNotFoundException)
        {
            return false;
        }
    }

    public async Task<IDictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken ct = default)
    {
        var result = new Dictionary<string, T?>();

        foreach (var key in keys)
        {
            result[key] = await GetAsync<T>(key, ct);
        }

        return result;
    }

    public async Task<int> RemoveManyAsync(IEnumerable<string> keys, CancellationToken ct = default)
    {
        var count = 0;

        foreach (var key in keys)
        {
            if (await RemoveAsync(key, ct))
            {
                count++;
            }
        }

        return count;
    }

    private string GetPrefixedKey(string key)
    {
        return string.IsNullOrEmpty(_cacheOptions.KeyPrefix)
            ? key
            : $"{_cacheOptions.KeyPrefix}.{key}";
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_connectionTask.IsValueCreated)
        {
            var (connection, _) = await _connectionTask.Value;
            await connection.DisposeAsync();
        }
    }
}