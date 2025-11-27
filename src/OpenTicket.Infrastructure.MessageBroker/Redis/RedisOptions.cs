namespace OpenTicket.Infrastructure.MessageBroker.Redis;

/// <summary>
/// Redis-specific configuration options.
/// </summary>
public class RedisOptions
{
    public const string SectionName = "MessageBroker:Redis";

    /// <summary>
    /// Redis connection string.
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Prefix for all Redis keys.
    /// </summary>
    public string KeyPrefix { get; set; } = "msgbroker";

    /// <summary>
    /// Maximum length of streams (0 for unlimited).
    /// </summary>
    public int MaxStreamLength { get; set; } = 100000;

    /// <summary>
    /// Timeout for claiming pending messages from dead consumers.
    /// </summary>
    public TimeSpan ClaimTimeout { get; set; } = TimeSpan.FromMinutes(5);
}
