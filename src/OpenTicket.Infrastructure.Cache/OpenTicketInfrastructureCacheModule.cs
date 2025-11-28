using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTicket.Infrastructure.Cache.Abstractions;
using OpenTicket.Infrastructure.Cache.InMemory;
using OpenTicket.Infrastructure.Cache.Nats;
using OpenTicket.Infrastructure.Cache.Redis;

namespace OpenTicket.Infrastructure.Cache;

/// <summary>
/// Provides extension methods for registering cache services.
/// </summary>
public static class OpenTicketInfrastructureCacheModule
{
    /// <summary>
    /// Adds distributed cache services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="provider">The cache provider to use.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCache(
        this IServiceCollection services,
        IConfiguration configuration,
        CacheProvider provider)
    {
        // Register common cache options
        services.Configure<CacheOptions>(
            configuration.GetSection(CacheOptions.SectionName));

        return provider switch
        {
            CacheProvider.InMemory => AddInMemoryCache(services),
            CacheProvider.Redis => AddRedisCache(services, configuration),
            CacheProvider.NatsKv => AddNatsKvCache(services, configuration),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown cache provider")
        };
    }

    /// <summary>
    /// Adds distributed cache services with custom options configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="provider">The cache provider to use.</param>
    /// <param name="configure">Action to configure cache options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCache(
        this IServiceCollection services,
        IConfiguration configuration,
        CacheProvider provider,
        Action<CacheOptions> configure)
    {
        services.Configure<CacheOptions>(
            configuration.GetSection(CacheOptions.SectionName));
        services.PostConfigure(configure);

        return provider switch
        {
            CacheProvider.InMemory => AddInMemoryCache(services),
            CacheProvider.Redis => AddRedisCache(services, configuration),
            CacheProvider.NatsKv => AddNatsKvCache(services, configuration),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown cache provider")
        };
    }

    private static IServiceCollection AddInMemoryCache(IServiceCollection services)
    {
        services.AddSingleton<IDistributedCache, InMemoryCache>();
        return services;
    }

    private static IServiceCollection AddRedisCache(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedisCacheOptions>(
            configuration.GetSection(RedisCacheOptions.SectionName));
        services.AddSingleton<IDistributedCache, RedisCache>();
        return services;
    }

    private static IServiceCollection AddNatsKvCache(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NatsKvCacheOptions>(
            configuration.GetSection(NatsKvCacheOptions.SectionName));
        services.AddSingleton<IDistributedCache, NatsKvCache>();
        return services;
    }
}