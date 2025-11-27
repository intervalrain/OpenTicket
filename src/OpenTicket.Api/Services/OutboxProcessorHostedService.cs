using OpenTicket.Application.Contracts.Notes.Events;
using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Ddd.Application.IntegrationEvents.Internal;
using OpenTicket.Ddd.Application.IntegrationEvents.Outbox;

namespace OpenTicket.Api.Services;

/// <summary>
/// Background service that processes outbox messages and dispatches them to handlers.
/// </summary>
public sealed class OutboxProcessorHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessorHostedService> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);

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

        var messages = await outboxRepository.GetPendingAsync(10, ct);

        if (messages.Count == 0)
            return;

        _logger.LogDebug("Processing {Count} outbox messages", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                await DispatchEventAsync(scope.ServiceProvider, message, ct);
                await outboxRepository.MarkAsPublishedAsync(message.Id, ct);

                _logger.LogInformation(
                    "Processed outbox message {MessageId} for event {EventType}",
                    message.Id,
                    message.EventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox message {MessageId}", message.Id);
                await outboxRepository.MarkAsFailedAsync(message.Id, ex.Message, ct);
            }
        }
    }

    private async Task DispatchEventAsync(IServiceProvider sp, OutboxMessage message, CancellationToken ct)
    {
        // Dispatch based on event type
        switch (message.EventType)
        {
            case "NoteCreatedEvent":
                var createdEvent = IntegrationEventSerializer.Deserialize<NoteCreatedEvent>(message.Payload);
                var createdHandler = sp.GetRequiredService<IIntegrationEventHandler<NoteCreatedEvent>>();
                await createdHandler.HandleAsync(createdEvent!, ct);
                break;

            case "NoteUpdatedEvent":
                var updatedEvent = IntegrationEventSerializer.Deserialize<NoteUpdatedEvent>(message.Payload);
                var updatedHandler = sp.GetRequiredService<IIntegrationEventHandler<NoteUpdatedEvent>>();
                await updatedHandler.HandleAsync(updatedEvent!, ct);
                break;

            case "NotePatchedEvent":
                var patchedEvent = IntegrationEventSerializer.Deserialize<NotePatchedEvent>(message.Payload);
                var patchedHandler = sp.GetRequiredService<IIntegrationEventHandler<NotePatchedEvent>>();
                await patchedHandler.HandleAsync(patchedEvent!, ct);
                break;

            default:
                _logger.LogWarning("Unknown event type: {EventType}", message.EventType);
                break;
        }
    }
}
