namespace OpenTicket.Infrastructure.Cache.Abstractions;

/// <summary>
/// Configuration options for the distributed cache.
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Cache";

    /// <summary>
    /// Default time-to-live for cached items.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Key prefix for all cache entries.
    /// Useful for multi-tenant scenarios or environment separation.
    /// </summary>
    public string KeyPrefix { get; set; } = string.Empty;
}