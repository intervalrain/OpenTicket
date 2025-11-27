using Microsoft.Extensions.DependencyInjection;
using OpenTicket.Ddd.Application.IntegrationEvents.Outbox;

namespace OpenTicket.Ddd.Application.IntegrationEvents;

/// <summary>
/// Extension methods for registering integration event services.
/// </summary>
public static class IntegrationEventServiceCollectionExtensions
{
    /// <summary>
    /// Adds integration event handler registration.
    /// </summary>
    /// <typeparam name="TEvent">The type of event.</typeparam>
    /// <typeparam name="THandler">The handler implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(
        this IServiceCollection services)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
        return services;
    }

    /// <summary>
    /// Configures outbox options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection ConfigureOutbox(
        this IServiceCollection services,
        Action<OutboxOptions> configure)
    {
        services.Configure(configure);
        return services;
    }
}
