using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTicket.Infrastructure.Cache.Abstractions;
using StackExchange.Redis;

namespace OpenTicket.Infrastructure.Cache.Redis;

/// <summary>
/// Redis implementation of IDistributedCache.
/// Provides distributed caching for production multi-instance deployments.
/// </summary>
public sealed class RedisCache : IDistributedCache, IAsyncDisposable
{
    private readonly CacheOptions _cacheOptions;
    private readonly RedisCacheOptions _redisOptions;
    private readonly ILogger<RedisCache> _logger;
    private readonly Lazy<Task<IConnectionMultiplexer>> _connectionTask;
    private bool _disposed;

    public RedisCache(
        IOptions<CacheOptions> cacheOptions,
        IOptions<RedisCacheOptions> redisOptions,
        ILogger<RedisCache> logger)
    {
        _cacheOptions = cacheOptions.Value;
        _redisOptions = redisOptions.Value;
        _logger = logger;
        _connectionTask = new Lazy<Task<IConnectionMultiplexer>>(ConnectAsync);
    }

    private async Task<IConnectionMultiplexer> ConnectAsync()
    {
        var options = ConfigurationOptions.Parse(_redisOptions.ConnectionString);
        options.ConnectTimeout = _redisOptions.ConnectTimeout;
        options.SyncTimeout = _redisOptions.SyncTimeout;

        _logger.LogInformation("Connecting to Redis at {ConnectionString}", _redisOptions.ConnectionString);

        return await ConnectionMultiplexer.ConnectAsync(options);
    }

    private async Task<IDatabase> GetDatabaseAsync()
    {
        var connection = await _connectionTask.Value;
        return connection.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var prefixedKey = GetPrefixedKey(key);
        var db = await GetDatabaseAsync();
        var value = await db.StringGetAsync(prefixedKey);

        if (value.IsNullOrEmpty)
            return default;

        return JsonSerializer.Deserialize<T>(value!);
    }

    public async Task<string?> GetStringAsync(string key, CancellationToken ct = default)
    {
        var prefixedKey = GetPrefixedKey(key);
        var db = await GetDatabaseAsync();
        var value = await db.StringGetAsync(prefixedKey);

        return value.IsNullOrEmpty ? null : value.ToString();
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var prefixedKey = GetPrefixedKey(key);
        var effectiveTtl = ttl ?? _cacheOptions.DefaultTtl;
        var serialized = JsonSerializer.Serialize(value);

        var db = await GetDatabaseAsync();
        await db.StringSetAsync(prefixedKey, serialized, effectiveTtl);
    }

    public async Task SetStringAsync(string key, string value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var prefixedKey = GetPrefixedKey(key);
        var effectiveTtl = ttl ?? _cacheOptions.DefaultTtl;

        var db = await GetDatabaseAsync();
        await db.StringSetAsync(prefixedKey, value, effectiveTtl);
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken ct = default)
    {
        var prefixedKey = GetPrefixedKey(key);
        var db = await GetDatabaseAsync();
        return await db.KeyDeleteAsync(prefixedKey);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        var prefixedKey = GetPrefixedKey(key);
        var db = await GetDatabaseAsync();
        return await db.KeyExistsAsync(prefixedKey);
    }

    public async Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        var prefixedKey = GetPrefixedKey(key);
        var serialized = JsonSerializer.Serialize(value);

        var db = await GetDatabaseAsync();
        // SET key value EX ttl NX - atomic operation
        return await db.StringSetAsync(prefixedKey, serialized, ttl, When.NotExists);
    }

    public async Task<bool> RefreshAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        var prefixedKey = GetPrefixedKey(key);
        var db = await GetDatabaseAsync();
        return await db.KeyExpireAsync(prefixedKey, ttl);
    }

    public async Task<IDictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken ct = default)
    {
        var keyList = keys.ToList();
        var prefixedKeys = keyList.Select(k => (RedisKey)GetPrefixedKey(k)).ToArray();

        var db = await GetDatabaseAsync();
        var values = await db.StringGetAsync(prefixedKeys);

        var result = new Dictionary<string, T?>();
        for (int i = 0; i < keyList.Count; i++)
        {
            if (!values[i].IsNullOrEmpty)
            {
                result[keyList[i]] = JsonSerializer.Deserialize<T>(values[i]!);
            }
            else
            {
                result[keyList[i]] = default;
            }
        }

        return result;
    }

    public async Task<int> RemoveManyAsync(IEnumerable<string> keys, CancellationToken ct = default)
    {
        var prefixedKeys = keys.Select(k => (RedisKey)GetPrefixedKey(k)).ToArray();

        var db = await GetDatabaseAsync();
        var deleted = await db.KeyDeleteAsync(prefixedKeys);

        return (int)deleted;
    }

    private string GetPrefixedKey(string key)
    {
        var prefix = _redisOptions.InstanceName;

        if (!string.IsNullOrEmpty(_cacheOptions.KeyPrefix))
        {
            prefix = $"{prefix}{_cacheOptions.KeyPrefix}:";
        }

        return $"{prefix}{key}";
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_connectionTask.IsValueCreated)
        {
            var connection = await _connectionTask.Value;
            await connection.CloseAsync();
            connection.Dispose();
        }
    }
}
