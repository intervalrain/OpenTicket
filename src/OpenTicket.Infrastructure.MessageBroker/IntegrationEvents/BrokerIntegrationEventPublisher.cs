using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Infrastructure.MessageBroker.Abstractions;

namespace OpenTicket.Infrastructure.MessageBroker.IntegrationEvents;

/// <summary>
/// Integration event publisher that sends events through the message broker (NATS/Redis).
/// Used by OutboxProcessor to publish events after they are stored in the outbox.
/// </summary>
public sealed class BrokerIntegrationEventPublisher
{
    private readonly IMessageBroker _messageBroker;
    private readonly IntegrationEventBrokerOptions _options;
    private readonly ILogger<BrokerIntegrationEventPublisher> _logger;

    public BrokerIntegrationEventPublisher(
        IMessageBroker messageBroker,
        IOptions<IntegrationEventBrokerOptions> options,
        ILogger<BrokerIntegrationEventPublisher> logger)
    {
        _messageBroker = messageBroker;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Publishes an integration event message to the message broker.
    /// </summary>
    /// <param name="message">The integration event message from outbox.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task PublishAsync(IntegrationEventMessage message, CancellationToken ct = default)
    {
        var brokerMessage = new IntegrationEventBrokerMessage
        {
            MessageId = message.MessageId,
            EventId = message.EventId,
            EventType = message.EventType,
            AggregateId = message.AggregateId,
            Payload = message.Payload,
            CorrelationId = message.CorrelationId,
            OccurredAt = message.OccurredAt,
            CreatedAt = message.CreatedAt
        };

        await _messageBroker.EnsureTopicExistsAsync(_options.Topic, ct);
        await _messageBroker.PublishAsync(_options.Topic, brokerMessage, ct);

        _logger.LogDebug(
            "Published integration event {EventType} ({EventId}) to topic {Topic}",
            message.EventType,
            message.EventId,
            _options.Topic);
    }
}

/// <summary>
/// Options for integration event broker configuration.
/// </summary>
public class IntegrationEventBrokerOptions
{
    /// <summary>
    /// Section name in configuration.
    /// </summary>
    public const string SectionName = "IntegrationEvents";

    /// <summary>
    /// The topic/stream name for integration events.
    /// Default: "integration-events"
    /// </summary>
    public string Topic { get; set; } = "integration-events";

    /// <summary>
    /// Consumer group name for this service.
    /// Should be unique per service type.
    /// </summary>
    public string ConsumerGroup { get; set; } = "default-consumer";
}
