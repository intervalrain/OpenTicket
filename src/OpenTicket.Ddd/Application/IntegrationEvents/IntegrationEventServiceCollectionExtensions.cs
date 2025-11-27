using System.Reflection;
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
    /// Scans the specified assemblies and registers all IIntegrationEventHandler implementations.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">The assemblies to scan for handlers.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIntegrationEventHandlersFromAssemblies(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        var handlerInterfaceType = typeof(IIntegrationEventHandler<>);

        foreach (var assembly in assemblies)
        {
            var handlerTypes = assembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false })
                .Where(t => t.GetInterfaces().Any(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterfaceType));

            foreach (var handlerType in handlerTypes)
            {
                var implementedInterfaces = handlerType.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterfaceType);

                foreach (var serviceType in implementedInterfaces)
                {
                    services.AddScoped(serviceType, handlerType);
                }
            }
        }

        return services;
    }

    /// <summary>
    /// Scans the assembly containing the specified marker type and registers all IIntegrationEventHandler implementations.
    /// </summary>
    /// <typeparam name="TMarker">A type from the assembly to scan.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIntegrationEventHandlersFromAssemblyContaining<TMarker>(
        this IServiceCollection services)
    {
        return services.AddIntegrationEventHandlersFromAssemblies(typeof(TMarker).Assembly);
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
