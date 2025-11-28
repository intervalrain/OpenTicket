using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Ddd.Application.IntegrationEvents.Idempotency;
using OpenTicket.Ddd.Application.IntegrationEvents.Internal;
using OpenTicket.Infrastructure.MessageBroker.Abstractions;

namespace OpenTicket.Infrastructure.MessageBroker.IntegrationEvents;

/// <summary>
/// Integration event subscriber that receives events from the message broker (NATS/Redis).
/// Handles deserialization, idempotency, and dispatching to registered handlers.
/// </summary>
public sealed class BrokerIntegrationEventSubscriber : IIntegrationEventSubscriber
{
    private readonly IMessageBroker _messageBroker;
    private readonly IServiceProvider _serviceProvider;
    private readonly IntegrationEventTypeRegistry _typeRegistry;
    private readonly IntegrationEventBrokerOptions _options;
    private readonly ILogger<BrokerIntegrationEventSubscriber> _logger;

    public BrokerIntegrationEventSubscriber(
        IMessageBroker messageBroker,
        IServiceProvider serviceProvider,
        IntegrationEventTypeRegistry typeRegistry,
        IOptions<IntegrationEventBrokerOptions> options,
        ILogger<BrokerIntegrationEventSubscriber> logger)
    {
        _messageBroker = messageBroker;
        _serviceProvider = serviceProvider;
        _typeRegistry = typeRegistry;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SubscribeAsync<TEvent>(
        string topic,
        string consumerGroup,
        CancellationToken ct = default)
        where TEvent : IIntegrationEvent
    {
        _typeRegistry.Register<TEvent>();

        await _messageBroker.SubscribeAsync<IntegrationEventBrokerMessage>(
            topic,
            consumerGroup,
            async (context, token) =>
            {
                var message = context.Message;

                // Only process if it's the expected event type
                if (message.EventType != typeof(TEvent).Name)
                {
                    await context.AckAsync(token);
                    return;
                }

                await ProcessMessageAsync<TEvent>(message, consumerGroup, context, token);
            },
            ct);
    }

    /// <inheritdoc />
    public async Task SubscribeAllAsync(
        string topic,
        string consumerGroup,
        CancellationToken ct = default)
    {
        await _messageBroker.SubscribeAsync<IntegrationEventBrokerMessage>(
            topic,
            consumerGroup,
            async (context, token) =>
            {
                var message = context.Message;
                await ProcessMessageDynamicAsync(message, consumerGroup, context, token);
            },
            ct);
    }

    private async Task ProcessMessageAsync<TEvent>(
        IntegrationEventBrokerMessage message,
        string consumerGroup,
        IMessageContext<IntegrationEventBrokerMessage> context,
        CancellationToken ct)
        where TEvent : IIntegrationEvent
    {
        using var scope = _serviceProvider.CreateScope();

        // Check idempotency
        var idempotencyService = scope.ServiceProvider.GetService<IIdempotencyService>();
        if (idempotencyService != null)
        {
            var alreadyProcessed = await idempotencyService.HasBeenProcessedAsync(
                message.EventId, consumerGroup, ct);

            if (alreadyProcessed)
            {
                _logger.LogDebug(
                    "Event {EventId} ({EventType}) already processed by {ConsumerGroup}, skipping",
                    message.EventId, message.EventType, consumerGroup);
                await context.AckAsync(ct);
                return;
            }
        }

        try
        {
            // Deserialize the event
            var @event = JsonSerializer.Deserialize<TEvent>(message.Payload);
            if (@event == null)
            {
                _logger.LogWarning(
                    "Failed to deserialize event {EventId} ({EventType})",
                    message.EventId, message.EventType);
                await context.AckAsync(ct);
                return;
            }

            // Get all handlers for this event type
            var handlers = scope.ServiceProvider.GetServices<IIntegrationEventHandler<TEvent>>();
            var handlerList = handlers.ToList();

            if (handlerList.Count == 0)
            {
                _logger.LogDebug(
                    "No handlers registered for event {EventType}",
                    message.EventType);
                await context.AckAsync(ct);
                return;
            }

            // Invoke all handlers
            foreach (var handler in handlerList)
            {
                await handler.HandleAsync(@event, ct);
            }

            // Mark as processed
            if (idempotencyService != null)
            {
                await idempotencyService.MarkAsProcessedAsync(
                    message.EventId, message.EventType, consumerGroup, ct);
            }

            await context.AckAsync(ct);

            _logger.LogDebug(
                "Successfully processed event {EventId} ({EventType}) with {HandlerCount} handler(s)",
                message.EventId, message.EventType, handlerList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing event {EventId} ({EventType})",
                message.EventId, message.EventType);

            // Requeue for retry
            await context.NakAsync(requeue: true, ct);
        }
    }

    private async Task ProcessMessageDynamicAsync(
        IntegrationEventBrokerMessage message,
        string consumerGroup,
        IMessageContext<IntegrationEventBrokerMessage> context,
        CancellationToken ct)
    {
        var eventType = _typeRegistry.GetType(message.EventType);
        if (eventType == null)
        {
            _logger.LogWarning(
                "Unknown event type {EventType}, skipping",
                message.EventType);
            await context.AckAsync(ct);
            return;
        }

        using var scope = _serviceProvider.CreateScope();

        // Check idempotency
        var idempotencyService = scope.ServiceProvider.GetService<IIdempotencyService>();
        if (idempotencyService != null)
        {
            var alreadyProcessed = await idempotencyService.HasBeenProcessedAsync(
                message.EventId, consumerGroup, ct);

            if (alreadyProcessed)
            {
                _logger.LogDebug(
                    "Event {EventId} ({EventType}) already processed by {ConsumerGroup}, skipping",
                    message.EventId, message.EventType, consumerGroup);
                await context.AckAsync(ct);
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
                await context.AckAsync(ct);
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
                await context.AckAsync(ct);
                return;
            }

            // Invoke all handlers via reflection
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

            await context.AckAsync(ct);

            _logger.LogDebug(
                "Successfully processed event {EventId} ({EventType}) with {HandlerCount} handler(s)",
                message.EventId, message.EventType, handlers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing event {EventId} ({EventType})",
                message.EventId, message.EventType);

            // Requeue for retry
            await context.NakAsync(requeue: true, ct);
        }
    }
}
