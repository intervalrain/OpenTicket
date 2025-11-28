using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using OpenTicket.Ddd.Application.Cqrs.Audit;
using OpenTicket.Ddd.Application.Cqrs.Authorization;
using OpenTicket.Ddd.Application.Cqrs.Behaviors;
using OpenTicket.Ddd.Application.Cqrs.Internal;
using OpenTicket.Ddd.Application.Cqrs.Validation;
using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Ddd.Application.IntegrationEvents.Idempotency;
using OpenTicket.Ddd.Application.IntegrationEvents.Internal;
using OpenTicket.Ddd.Application.IntegrationEvents.Outbox;

namespace OpenTicket.Ddd.Application.Cqrs;

/// <summary>
/// Extension methods for registering CQRS services.
/// </summary>
public static class CqrsServiceCollectionExtensions
{
    /// <summary>
    /// Adds the CQRS dispatcher to the service collection.
    /// </summary>
    public static IServiceCollection AddCqrs(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        return services;
    }

    /// <summary>
    /// Adds the CQRS dispatcher and registers all handlers from the specified assembly.
    /// </summary>
    public static IServiceCollection AddCqrs(this IServiceCollection services, Assembly assembly)
    {
        services.AddCqrs();
        services.AddHandlersFromAssembly(assembly);
        return services;
    }

    /// <summary>
    /// Adds the CQRS dispatcher and registers all handlers from the specified assemblies.
    /// </summary>
    public static IServiceCollection AddCqrs(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddCqrs();
        foreach (var assembly in assemblies)
        {
            services.AddHandlersFromAssembly(assembly);
        }
        return services;
    }

    /// <summary>
    /// Registers all command and query handlers from the specified assembly.
    /// </summary>
    public static IServiceCollection AddHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && IsHandlerInterface(i.GetGenericTypeDefinition()))
                .Select(i => new { Implementation = t, Interface = i }));

        foreach (var handler in handlerTypes)
        {
            services.AddScoped(handler.Interface, handler.Implementation);
        }

        return services;
    }

    /// <summary>
    /// Registers a pipeline behavior.
    /// </summary>
    public static IServiceCollection AddPipelineBehavior<TBehavior>(this IServiceCollection services)
        where TBehavior : class
    {
        var behaviorType = typeof(TBehavior);
        var interfaces = behaviorType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));

        foreach (var @interface in interfaces)
        {
            services.AddScoped(@interface, behaviorType);
        }

        return services;
    }

    /// <summary>
    /// Registers a generic pipeline behavior that applies to all requests.
    /// </summary>
    public static IServiceCollection AddPipelineBehavior(
        this IServiceCollection services,
        Type behaviorType)
    {
        if (!behaviorType.IsGenericTypeDefinition)
            throw new ArgumentException("Behavior type must be an open generic type", nameof(behaviorType));

        services.AddScoped(typeof(IPipelineBehavior<,>), behaviorType);
        return services;
    }

    private static bool IsHandlerInterface(Type type)
    {
        return type == typeof(ICommandHandler<,>) || type == typeof(IQueryHandler<,>);
    }

    /// <summary>
    /// Registers all validators from the specified assembly.
    /// </summary>
    public static IServiceCollection AddValidatorsFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        var validatorTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>))
                .Select(i => new { Implementation = t, Interface = i }));

        foreach (var validator in validatorTypes)
        {
            services.AddScoped(validator.Interface, validator.Implementation);
        }

        return services;
    }

    /// <summary>
    /// Adds the logging pipeline behavior.
    /// </summary>
    public static IServiceCollection AddLoggingBehavior(this IServiceCollection services)
    {
        services.AddPipelineBehavior(typeof(LoggingBehavior<,>));
        return services;
    }

    /// <summary>
    /// Adds the validation pipeline behavior.
    /// Requires validators to be registered via AddValidatorsFromAssembly.
    /// </summary>
    public static IServiceCollection AddValidationBehavior(this IServiceCollection services)
    {
        services.AddPipelineBehavior(typeof(ValidationBehavior<,>));
        return services;
    }

    /// <summary>
    /// Adds the transaction pipeline behavior.
    /// Requires IUnitOfWork to be registered.
    /// </summary>
    public static IServiceCollection AddTransactionBehavior(this IServiceCollection services)
    {
        services.AddPipelineBehavior(typeof(TransactionBehavior<,>));
        return services;
    }

    /// <summary>
    /// Adds the authorization pipeline behavior for AOP-style authorization.
    /// Requires IRequestAuthorizationService to be registered.
    /// </summary>
    /// <remarks>
    /// This behavior only applies to requests that implement
    /// <see cref="IAuthorizeableRequest{TResponse}"/> and are decorated with
    /// <see cref="AuthorizeAttribute"/>.
    /// </remarks>
    public static IServiceCollection AddAuthorizationBehavior(this IServiceCollection services)
    {
        services.AddPipelineBehavior(typeof(AuthorizationBehavior<,>));
        return services;
    }

    /// <summary>
    /// Adds the audit pipeline behavior with trace ID support.
    /// </summary>
    public static IServiceCollection AddAuditBehavior(this IServiceCollection services)
    {
        services.AddScoped<IAuditContext, AuditContext>();
        services.AddPipelineBehavior(typeof(AuditBehavior<,>));
        return services;
    }

    /// <summary>
    /// Adds all standard pipeline behaviors in the recommended order:
    /// 1. Audit (trace ID setup)
    /// 2. Logging
    /// 3. Authorization (AOP-style, for requests with [Authorize] attribute)
    /// 4. Validation
    /// 5. Transaction
    /// </summary>
    /// <remarks>
    /// Note: Authorization behavior requires <see cref="IRequestAuthorizationService"/>
    /// to be registered separately (typically in the Application module).
    /// </remarks>
    public static IServiceCollection AddStandardBehaviors(this IServiceCollection services)
    {
        services.AddAuditBehavior();
        services.AddLoggingBehavior();
        services.AddAuthorizationBehavior();
        services.AddValidationBehavior();
        services.AddTransactionBehavior();
        return services;
    }

    /// <summary>
    /// Adds integration events infrastructure with outbox pattern.
    /// </summary>
    public static IServiceCollection AddIntegrationEvents(this IServiceCollection services)
    {
        // Outbox infrastructure
        services.AddSingleton<IOutboxRepository, InMemoryOutboxRepository>();
        services.AddSingleton<IIdempotencyService, InMemoryIdempotencyService>();

        // Event publisher
        services.AddSingleton<IIntegrationEventPublisher, OutboxIntegrationEventPublisher>();

        return services;
    }
}
