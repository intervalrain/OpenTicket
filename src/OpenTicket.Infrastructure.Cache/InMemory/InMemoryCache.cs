using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenTicket.Infrastructure.Cache.Abstractions;

namespace OpenTicket.Infrastructure.Cache.InMemory;

/// <summary>
/// In-memory implementation of IDistributedCache.
/// Suitable for MVP mode and single-instance deployments.
/// Not distributed - each instance has its own cache.
/// </summary>
public sealed class InMemoryCache : IDistributedCache, IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly CacheOptions _options;
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public InMemoryCache(IOptions<CacheOptions> options)
    {
        _options = options.Value;
        // Cleanup expired entries every minute
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var prefixedKey = GetPrefixedKey(key);

        if (_cache.TryGetValue(prefixedKey, out var entry) && !entry.IsExpired)
        {
            return Task.FromResult(JsonSerializer.Deserialize<T>(entry.Value));
        }

        // Remove expired entry
        if (entry?.IsExpired == true)
        {
            _cache.TryRemove(prefixedKey, out _);
        }

        return Task.FromResult(default(T));
    }

    public Task<string?> GetStringAsync(string key, CancellationToken ct = default)
    {
        var prefixedKey = GetPrefixedKey(key);

        if (_cache.TryGetValue(prefixedKey, out var entry) && !entry.IsExpired)
        {
            return Task.FromResult<string?>(entry.Value);
        }

        // Remove expired entry
        if (entry?.IsExpired == true)
        {
            _cache.TryRemove(prefixedKey, out _);
        }

        return Task.FromResult<string?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var prefixedKey = GetPrefixedKey(key);
        var effectiveTtl = ttl ?? _options.DefaultTtl;
        var serialized = JsonSerializer.Serialize(value);

        _cache[prefixedKey] = new CacheEntry(serialized, DateTime.UtcNow.Add(effectiveTtl));

        return Task.CompletedTask;
    }

    public Task SetStringAsync(string key, string value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var prefixedKey = GetPrefixedKey(key);
        var effectiveTtl = ttl ?? _options.DefaultTtl;

        _cache[prefixedKey] = new CacheEntry(value, DateTime.UtcNow.Add(effectiveTtl));

        return Task.CompletedTask;
    }

    public Task<bool> RemoveAsync(string key, CancellationToken ct = default)
    {
        var prefixedKey = GetPrefixedKey(key);
        return Task.FromResult(_cache.TryRemove(prefixedKey, out _));
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        var prefixedKey = GetPrefixedKey(key);

        if (_cache.TryGetValue(prefixedKey, out var entry))
        {
            if (entry.IsExpired)
            {
                _cache.TryRemove(prefixedKey, out _);
                return Task.FromResult(false);
            }
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        var prefixedKey = GetPrefixedKey(key);
        var serialized = JsonSerializer.Serialize(value);
        var newEntry = new CacheEntry(serialized, DateTime.UtcNow.Add(ttl));

        // Try to add atomically
        if (_cache.TryAdd(prefixedKey, newEntry))
        {
            return Task.FromResult(true);
        }

        // Key exists, check if expired
        if (_cache.TryGetValue(prefixedKey, out var existingEntry) && existingEntry.IsExpired)
        {
            // Try to replace expired entry
            if (_cache.TryUpdate(prefixedKey, newEntry, existingEntry))
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    public Task<bool> RefreshAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        var prefixedKey = GetPrefixedKey(key);

        if (_cache.TryGetValue(prefixedKey, out var entry) && !entry.IsExpired)
        {
            var refreshedEntry = new CacheEntry(entry.Value, DateTime.UtcNow.Add(ttl));
            _cache.TryUpdate(prefixedKey, refreshedEntry, entry);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
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

    public Task<int> RemoveManyAsync(IEnumerable<string> keys, CancellationToken ct = default)
    {
        var count = 0;

        foreach (var key in keys)
        {
            var prefixedKey = GetPrefixedKey(key);
            if (_cache.TryRemove(prefixedKey, out _))
            {
                count++;
            }
        }

        return Task.FromResult(count);
    }

    private string GetPrefixedKey(string key)
    {
        return string.IsNullOrEmpty(_options.KeyPrefix)
            ? key
            : $"{_options.KeyPrefix}:{key}";
    }

    private void CleanupExpiredEntries(object? state)
    {
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cleanupTimer.Dispose();
        _cache.Clear();
    }

    private sealed record CacheEntry(string Value, DateTime ExpiresAt)
    {
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    }
}
