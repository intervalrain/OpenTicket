using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTicket.Infrastructure.Resilience.Abstractions;
using OpenTicket.Infrastructure.Resilience.Polly;

namespace OpenTicket.Infrastructure.Resilience;

public static class OpenTicketInfrastructureResilienceModule
{
    /// <summary>
    /// Adds resilience services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddResilience(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ResilienceOptions>(
            configuration.GetSection(ResilienceOptions.SectionName));

        services.AddSingleton<IResilienceService, PollyResilienceService>();

        return services;
    }

    /// <summary>
    /// Adds resilience services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="configure">Additional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddResilience(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<ResilienceOptions> configure)
    {
        services.Configure<ResilienceOptions>(
            configuration.GetSection(ResilienceOptions.SectionName));
        services.PostConfigure(configure);

        services.AddSingleton<IResilienceService, PollyResilienceService>();

        return services;
    }
}
