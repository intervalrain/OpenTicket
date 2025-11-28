using Microsoft.Extensions.Options;
using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Ddd.Application.IntegrationEvents.Internal;
using OpenTicket.Ddd.Application.IntegrationEvents.Outbox;
using OpenTicket.Infrastructure.MessageBroker.IntegrationEvents;

namespace OpenTicket.Api.Services;

/// <summary>
/// Background service that processes outbox messages and publishes them through the message broker.
/// For InMemory mode, events are dispatched directly to handlers.
/// For NATS/Redis mode, events are published to the message broker for distributed processing.
/// </summary>
public sealed class OutboxProcessorHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessorHostedService> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private DateTime _lastCleanup = DateTime.MinValue;

    public OutboxProcessorHostedService(
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessorHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);

                // Periodic cleanup
                if (DateTime.UtcNow - _lastCleanup > _cleanupInterval)
                {
                    await CleanupOldMessagesAsync(stoppingToken);
                    _lastCleanup = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("Outbox processor stopped");
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var outboxOptions = scope.ServiceProvider.GetRequiredService<IOptions<OutboxOptions>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OutboxProcessor>>();

        // Determine which publisher to use based on registered services
        Func<IntegrationEventMessage, CancellationToken, Task> publishAction;

        var inMemoryBus = scope.ServiceProvider.GetService<InMemoryIntegrationEventBus>();
        if (inMemoryBus != null)
        {
            // InMemory mode: dispatch directly through in-memory bus
            publishAction = inMemoryBus.PublishAsync;
            _logger.LogDebug("Using InMemory integration event bus");
        }
        else
        {
            // Broker mode: publish through message broker
            var brokerPublisher = scope.ServiceProvider.GetService<BrokerIntegrationEventPublisher>();
            if (brokerPublisher != null)
            {
                publishAction = brokerPublisher.PublishAsync;
                _logger.LogDebug("Using broker integration event publisher");
            }
            else
            {
                _logger.LogWarning("No integration event publisher configured, skipping outbox processing");
                return;
            }
        }

        var processor = new OutboxProcessor(
            outboxRepository,
            outboxOptions,
            logger,
            publishAction);

        var processedCount = await processor.ProcessAsync(ct);

        if (processedCount > 0)
        {
            _logger.LogDebug("Processed {Count} outbox messages", processedCount);
        }
    }

    private async Task CleanupOldMessagesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var outboxOptions = scope.ServiceProvider.GetRequiredService<IOptions<OutboxOptions>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OutboxProcessor>>();

        var processor = new OutboxProcessor(
            outboxRepository,
            outboxOptions,
            logger,
            (_, _) => Task.CompletedTask);

        var deletedCount = await processor.CleanupAsync(ct);

        if (deletedCount > 0)
        {
            _logger.LogInformation("Cleaned up {Count} old outbox messages", deletedCount);
        }
    }
}
