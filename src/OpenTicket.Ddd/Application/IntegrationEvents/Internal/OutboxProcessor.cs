using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTicket.Ddd.Application.IntegrationEvents.Outbox;

namespace OpenTicket.Ddd.Application.IntegrationEvents.Internal;

/// <summary>
/// Default implementation of IOutboxProcessor.
/// Processes pending outbox messages and publishes them using the provided publisher action.
/// </summary>
public sealed class OutboxProcessor : IOutboxProcessor
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly Func<IntegrationEventMessage, CancellationToken, Task> _publishAction;

    public OutboxProcessor(
        IOutboxRepository outboxRepository,
        IOptions<OutboxOptions> options,
        ILogger<OutboxProcessor> logger,
        Func<IntegrationEventMessage, CancellationToken, Task> publishAction)
    {
        _outboxRepository = outboxRepository;
        _options = options.Value;
        _logger = logger;
        _publishAction = publishAction;
    }

    public async Task<int> ProcessAsync(CancellationToken ct = default)
    {
        var messages = await _outboxRepository.GetPendingAsync(_options.BatchSize, ct);

        if (messages.Count == 0)
            return 0;

        _logger.LogDebug("Processing {Count} outbox messages", messages.Count);

        var processedCount = 0;

        foreach (var message in messages)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                var eventMessage = new IntegrationEventMessage
                {
                    MessageId = message.Id.ToString(),
                    EventId = message.EventId,
                    EventType = message.EventType,
                    AggregateId = message.AggregateId,
                    Payload = message.Payload,
                    CorrelationId = message.CorrelationId,
                    OccurredAt = message.CreatedAt,
                    CreatedAt = DateTime.UtcNow
                };

                await _publishAction(eventMessage, ct);

                message.MarkAsPublished();
                await _outboxRepository.UpdateAsync(message, ct);

                processedCount++;

                _logger.LogDebug(
                    "Published outbox message {MessageId} for event {EventType}",
                    message.Id,
                    message.EventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to publish outbox message {MessageId}, attempt {Attempt}/{MaxAttempts}",
                    message.Id,
                    message.RetryCount + 1,
                    _options.MaxRetryAttempts);

                if (message.RetryCount + 1 >= _options.MaxRetryAttempts)
                {
                    message.MarkAsFailed(ex.Message);
                }
                else
                {
                    message.IncrementRetry(ex.Message);
                }

                await _outboxRepository.UpdateAsync(message, ct);
            }
        }

        _logger.LogInformation(
            "Processed {ProcessedCount}/{TotalCount} outbox messages",
            processedCount,
            messages.Count);

        return processedCount;
    }

    public async Task<int> CleanupAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - _options.RetentionPeriod;
        var deletedCount = await _outboxRepository.DeletePublishedAsync(cutoff, ct);

        if (deletedCount > 0)
        {
            _logger.LogInformation(
                "Cleaned up {Count} published outbox messages older than {Cutoff}",
                deletedCount,
                cutoff);
        }

        return deletedCount;
    }
}
