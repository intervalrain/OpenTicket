using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Ddd.Application.IntegrationEvents.Idempotency;
using OpenTicket.Ddd.Application.IntegrationEvents.Internal;

namespace OpenTicket.Infrastructure.MessageBroker.IntegrationEvents;

/// <summary>
/// In-memory integration event bus for MVP mode.
/// Directly dispatches events to handlers without going through a real message broker.
/// Suitable for single-instance deployments and testing.
/// </summary>
public sealed class InMemoryIntegrationEventBus : IIntegrationEventSubscriber
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IntegrationEventTypeRegistry _typeRegistry;
    private readonly ILogger<InMemoryIntegrationEventBus> _logger;
    private readonly ConcurrentQueue<IntegrationEventMessage> _pendingMessages = new();
    private readonly ConcurrentDictionary<string, bool> _subscribedTopics = new();

    public InMemoryIntegrationEventBus(
        IServiceProvider serviceProvider,
        IntegrationEventTypeRegistry typeRegistry,
        ILogger<InMemoryIntegrationEventBus> logger)
    {
        _serviceProvider = serviceProvider;
        _typeRegistry = typeRegistry;
        _logger = logger;
    }

    /// <summary>
    /// Publishes an integration event message to the in-memory bus.
    /// The event is immediately dispatched to handlers.
    /// </summary>
    /// <param name="message">The integration event message.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task PublishAsync(IntegrationEventMessage message, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Publishing event {EventType} ({EventId}) to in-memory bus",
            message.EventType, message.EventId);

        // Dispatch immediately to handlers
        await DispatchToHandlersAsync(message, ct);
    }

    /// <inheritdoc />
    public Task SubscribeAsync<TEvent>(
        string topic,
        string consumerGroup,
        CancellationToken ct = default)
        where TEvent : IIntegrationEvent
    {
        _typeRegistry.Register<TEvent>();
        _subscribedTopics.TryAdd($"{topic}-{typeof(TEvent).Name}", true);

        _logger.LogInformation(
            "In-memory subscriber registered for {EventType} on topic {Topic}",
            typeof(TEvent).Name, topic);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SubscribeAllAsync(
        string topic,
        string consumerGroup,
        CancellationToken ct = default)
    {
        _subscribedTopics.TryAdd(topic, true);

        _logger.LogInformation(
            "In-memory subscriber registered for all events on topic {Topic}",
            topic);

        return Task.CompletedTask;
    }

    private async Task DispatchToHandlersAsync(IntegrationEventMessage message, CancellationToken ct)
    {
        var eventType = _typeRegistry.GetType(message.EventType);
        if (eventType == null)
        {
            _logger.LogWarning(
                "Unknown event type {EventType}, skipping dispatch",
                message.EventType);
            return;
        }

        using var scope = _serviceProvider.CreateScope();

        // Check idempotency
        var idempotencyService = scope.ServiceProvider.GetService<IIdempotencyService>();
        var consumerGroup = "in-memory";

        if (idempotencyService != null)
        {
            var alreadyProcessed = await idempotencyService.HasBeenProcessedAsync(
                message.EventId, consumerGroup, ct);

            if (alreadyProcessed)
            {
                _logger.LogDebug(
                    "Event {EventId} ({EventType}) already processed, skipping",
                    message.EventId, message.EventType);
                return;
            }
        }

        try
        {
            // Deserialize the event
            var @event = JsonSerializer.Deserialize(message.Payload, eventType);
            if (@event == null)
            {
                _logger.LogWarning(
                    "Failed to deserialize event {EventId} ({EventType})",
                    message.EventId, message.EventType);
                return;
            }

            // Get handler type and invoke
            var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
            var handlers = scope.ServiceProvider.GetServices(handlerType).ToList();

            if (handlers.Count == 0)
            {
                _logger.LogDebug(
                    "No handlers registered for event {EventType}",
                    message.EventType);
                return;
            }

            // Invoke all handlers
            var handleMethod = handlerType.GetMethod("HandleAsync");
            foreach (var handler in handlers)
            {
                if (handler != null && handleMethod != null)
                {
                    var task = (Task)handleMethod.Invoke(handler, [@event, ct])!;
                    await task;
                }
            }

            // Mark as processed
            if (idempotencyService != null)
            {
                await idempotencyService.MarkAsProcessedAsync(
                    message.EventId, message.EventType, consumerGroup, ct);
            }

            _logger.LogDebug(
                "Successfully dispatched event {EventId} ({EventType}) to {HandlerCount} handler(s)",
                message.EventId, message.EventType, handlers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error dispatching event {EventId} ({EventType})",
                message.EventId, message.EventType);
            throw;
        }
    }
}
