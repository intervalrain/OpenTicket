namespace OpenTicket.Infrastructure.Cache.Redis;

/// <summary>
/// Redis-specific cache configuration options.
/// </summary>
public class RedisCacheOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Cache:Redis";

    /// <summary>
    /// Redis connection string.
    /// Example: "localhost:6379" or "redis-server:6379,password=secret"
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Instance name prefix for all keys.
    /// Useful for separating environments or services.
    /// </summary>
    public string InstanceName { get; set; } = "OpenTicket:";

    /// <summary>
    /// Connection timeout in milliseconds.
    /// </summary>
    public int ConnectTimeout { get; set; } = 5000;

    /// <summary>
    /// Sync timeout in milliseconds.
    /// </summary>
    public int SyncTimeout { get; set; } = 1000;
}
