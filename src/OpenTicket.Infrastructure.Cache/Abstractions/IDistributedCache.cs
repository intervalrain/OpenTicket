namespace OpenTicket.Infrastructure.Cache.Abstractions;

/// <summary>
/// Distributed cache interface for high-performance, non-persistent caching.
/// Supports multiple providers: InMemory (MVP), Redis, NATS KV Store.
/// </summary>
public interface IDistributedCache
{
    /// <summary>
    /// Gets a value from the cache.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cached value, or default if not found.</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>
    /// Gets a string value from the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cached string, or null if not found.</returns>
    Task<string?> GetStringAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Sets a value in the cache with optional TTL.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="ttl">Optional time-to-live. If null, uses default TTL from options.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);

    /// <summary>
    /// Sets a string value in the cache with optional TTL.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The string value to cache.</param>
    /// <param name="ttl">Optional time-to-live. If null, uses default TTL from options.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetStringAsync(string key, string value, TimeSpan? ttl = null, CancellationToken ct = default);

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the key was removed, false if it didn't exist.</returns>
    Task<bool> RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Checks if a key exists in the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the key exists, false otherwise.</returns>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Sets a value only if the key does not already exist (atomic operation).
    /// Useful for distributed locking scenarios like seat locks.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="ttl">Time-to-live for the key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the value was set, false if the key already exists.</returns>
    Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Refreshes the TTL of an existing key without changing its value.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="ttl">New time-to-live.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the key was refreshed, false if it didn't exist.</returns>
    Task<bool> RefreshAsync(string key, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Gets multiple values from the cache.
    /// </summary>
    /// <typeparam name="T">The type of the cached values.</typeparam>
    /// <param name="keys">The cache keys.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary of key-value pairs for found keys.</returns>
    Task<IDictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken ct = default);

    /// <summary>
    /// Removes multiple values from the cache.
    /// </summary>
    /// <param name="keys">The cache keys to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of keys removed.</returns>
    Task<int> RemoveManyAsync(IEnumerable<string> keys, CancellationToken ct = default);
}