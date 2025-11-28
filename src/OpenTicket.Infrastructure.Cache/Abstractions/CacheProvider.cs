namespace OpenTicket.Infrastructure.Cache.Abstractions;

/// <summary>
/// Available cache provider options.
/// </summary>
public enum CacheProvider
{
    /// <summary>
    /// In-memory cache for MVP mode and testing.
    /// Single-instance only, not distributed.
    /// </summary>
    InMemory,

    /// <summary>
    /// Redis distributed cache.
    /// Recommended for production with multiple instances.
    /// </summary>
    Redis,

    /// <summary>
    /// NATS KV Store distributed cache.
    /// Good option for environments already using NATS.
    /// </summary>
    NatsKv
}
