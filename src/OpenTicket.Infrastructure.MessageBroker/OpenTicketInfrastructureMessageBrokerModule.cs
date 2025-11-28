using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Ddd.Application.IntegrationEvents.Idempotency;
using OpenTicket.Ddd.Application.IntegrationEvents.Internal;
using OpenTicket.Infrastructure.MessageBroker.Abstractions;
using OpenTicket.Infrastructure.MessageBroker.InMemory;
using OpenTicket.Infrastructure.MessageBroker.IntegrationEvents;
using OpenTicket.Infrastructure.MessageBroker.Nats;
using OpenTicket.Infrastructure.MessageBroker.Redis;

namespace OpenTicket.Infrastructure.MessageBroker;

public static class OpenTicketInfrastructureMessageBrokerModule
{
    /// <summary>
    /// Adds message broker services to the service collection.
    /// </summary>
    public static IServiceCollection AddMessageBroker(
        this IServiceCollection services,
        IConfiguration configuration,
        MessageBrokerOption option)
    {
        // Register common options
        services.Configure<MessageBrokerOptions>(
            configuration.GetSection(MessageBrokerOptions.SectionName));

        // Register integration event options
        services.Configure<IntegrationEventBrokerOptions>(
            configuration.GetSection(IntegrationEventBrokerOptions.SectionName));

        // Register type registry as singleton
        services.AddSingleton<IntegrationEventTypeRegistry>();

        // Register idempotency service
        services.AddSingleton<IIdempotencyService, InMemoryIdempotencyService>();

        return option switch
        {
            MessageBrokerOption.InMemory => AddInMemoryMessageBroker(services),
            MessageBrokerOption.Redis => AddRedisMessageBroker(services, configuration),
            MessageBrokerOption.Nats => AddNatsMessageBroker(services, configuration),
            MessageBrokerOption.RabbitMq => AddRabbitMqMessageBroker(services, configuration),
            MessageBrokerOption.Kafka => AddKafkaMessageBroker(services, configuration),
            _ => throw new ArgumentOutOfRangeException(nameof(option), option, "Unknown message broker option")
        };
    }

    /// <summary>
    /// Adds message broker services with custom configuration.
    /// </summary>
    public static IServiceCollection AddMessageBroker(
        this IServiceCollection services,
        IConfiguration configuration,
        MessageBrokerOption option,
        Action<MessageBrokerOptions> configure)
    {
        services.Configure<MessageBrokerOptions>(
            configuration.GetSection(MessageBrokerOptions.SectionName));
        services.PostConfigure(configure);

        // Register integration event options
        services.Configure<IntegrationEventBrokerOptions>(
            configuration.GetSection(IntegrationEventBrokerOptions.SectionName));

        // Register type registry as singleton
        services.AddSingleton<IntegrationEventTypeRegistry>();

        // Register idempotency service
        services.AddSingleton<IIdempotencyService, InMemoryIdempotencyService>();

        return option switch
        {
            MessageBrokerOption.InMemory => AddInMemoryMessageBroker(services),
            MessageBrokerOption.Redis => AddRedisMessageBroker(services, configuration),
            MessageBrokerOption.Nats => AddNatsMessageBroker(services, configuration),
            MessageBrokerOption.RabbitMq => AddRabbitMqMessageBroker(services, configuration),
            MessageBrokerOption.Kafka => AddKafkaMessageBroker(services, configuration),
            _ => throw new ArgumentOutOfRangeException(nameof(option), option, "Unknown message broker option")
        };
    }

    private static IServiceCollection AddInMemoryMessageBroker(IServiceCollection services)
    {
        services.AddSingleton<IMessageBroker, InMemoryMessageBroker>();
        services.AddSingleton<IMessageProducer>(sp => sp.GetRequiredService<IMessageBroker>());
        services.AddSingleton<IMessageConsumer>(sp => sp.GetRequiredService<IMessageBroker>());

        // For InMemory mode, use the InMemoryIntegrationEventBus
        services.AddSingleton<InMemoryIntegrationEventBus>();
        services.AddSingleton<IIntegrationEventSubscriber>(sp => sp.GetRequiredService<InMemoryIntegrationEventBus>());

        return services;
    }

    private static IServiceCollection AddRedisMessageBroker(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.AddSingleton<IMessageBroker, RedisMessageBroker>();
        services.AddSingleton<IMessageProducer>(sp => sp.GetRequiredService<IMessageBroker>());
        services.AddSingleton<IMessageConsumer>(sp => sp.GetRequiredService<IMessageBroker>());

        // Integration event publisher and subscriber
        services.AddSingleton<BrokerIntegrationEventPublisher>();
        services.AddSingleton<IIntegrationEventSubscriber, BrokerIntegrationEventSubscriber>();

        return services;
    }

    private static IServiceCollection AddNatsMessageBroker(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NatsOptions>(configuration.GetSection(NatsOptions.SectionName));
        services.AddSingleton<IMessageBroker, NatsMessageBroker>();
        services.AddSingleton<IMessageProducer>(sp => sp.GetRequiredService<IMessageBroker>());
        services.AddSingleton<IMessageConsumer>(sp => sp.GetRequiredService<IMessageBroker>());

        // Integration event publisher and subscriber
        services.AddSingleton<BrokerIntegrationEventPublisher>();
        services.AddSingleton<IIntegrationEventSubscriber, BrokerIntegrationEventSubscriber>();

        return services;
    }

    private static IServiceCollection AddRabbitMqMessageBroker(IServiceCollection services, IConfiguration configuration)
    {
        // TODO: Implement RabbitMQ message broker when ready
        throw new NotImplementedException("RabbitMQ message broker is not yet implemented");
    }

    private static IServiceCollection AddKafkaMessageBroker(IServiceCollection services, IConfiguration configuration)
    {
        // TODO: Implement Kafka message broker when ready
        throw new NotImplementedException("Kafka message broker is not yet implemented");
    }
}
