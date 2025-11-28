namespace OpenTicket.Infrastructure.Cache.Nats;

/// <summary>
/// NATS KV Store cache configuration options.
/// </summary>
public class NatsKvCacheOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Cache:NatsKv";

    /// <summary>
    /// NATS server URL.
    /// Example: "nats://localhost:4222"
    /// </summary>
    public string Url { get; set; } = "nats://localhost:4222";

    /// <summary>
    /// KV bucket name for cache entries.
    /// </summary>
    public string BucketName { get; set; } = "cache";

    /// <summary>
    /// Maximum number of history entries per key.
    /// Set to 1 for cache use cases (no history needed).
    /// </summary>
    public int MaxHistory { get; set; } = 1;

    /// <summary>
    /// Connection timeout.
    /// </summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);
}