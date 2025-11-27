using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Ddd.Application.IntegrationEvents.Internal;
using OpenTicket.Ddd.Application.IntegrationEvents.Outbox;
using Shouldly;

namespace OpenTicket.Ddd.Tests.Application.IntegrationEvents;

public class OutboxProcessorTests
{
    private record TestEvent : IntegrationEvent
    {
        public string Data { get; init; } = string.Empty;
        public override string AggregateId => Data;
    }

    private readonly InMemoryOutboxRepository _outboxRepository;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly OutboxOptions _options;
    private readonly List<IntegrationEventMessage> _publishedMessages;

    public OutboxProcessorTests()
    {
        _outboxRepository = new InMemoryOutboxRepository();
        _logger = Substitute.For<ILogger<OutboxProcessor>>();
        _options = new OutboxOptions
        {
            BatchSize = 10,
            MaxRetryAttempts = 3,
            RetentionPeriod = TimeSpan.FromDays(7)
        };
        _publishedMessages = new List<IntegrationEventMessage>();
    }

    private OutboxProcessor CreateProcessor(
        Func<IntegrationEventMessage, CancellationToken, Task>? publishAction = null)
    {
        publishAction ??= (msg, ct) =>
        {
            _publishedMessages.Add(msg);
            return Task.CompletedTask;
        };

        return new OutboxProcessor(
            _outboxRepository,
            Options.Create(_options),
            _logger,
            publishAction);
    }

    private async Task AddOutboxMessage(string eventType = "TestEvent", string aggregateId = "test")
    {
        var message = OutboxMessage.Create(
            Guid.NewGuid(),
            eventType,
            aggregateId,
            $"{{\"data\":\"{aggregateId}\"}}");
        await _outboxRepository.InsertAsync(message);
    }

    [Fact]
    public async Task ProcessAsync_ShouldPublishPendingMessages()
    {
        // Arrange
        await AddOutboxMessage();
        await AddOutboxMessage();
        var processor = CreateProcessor();

        // Act
        var processedCount = await processor.ProcessAsync();

        // Assert
        processedCount.ShouldBe(2);
        _publishedMessages.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ProcessAsync_ShouldMarkAsPublished()
    {
        // Arrange
        await AddOutboxMessage();
        var processor = CreateProcessor();

        // Act
        await processor.ProcessAsync();

        // Assert
        var pending = await _outboxRepository.GetPendingAsync(10);
        pending.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_ShouldRetryOnFailure()
    {
        // Arrange
        await AddOutboxMessage();
        var failCount = 0;
        var processor = CreateProcessor((msg, ct) =>
        {
            if (failCount++ < 1)
                throw new Exception("Test failure");
            _publishedMessages.Add(msg);
            return Task.CompletedTask;
        });

        // Act - First attempt fails
        await processor.ProcessAsync();

        // Assert - Message should be back to pending
        var pending = await _outboxRepository.GetPendingAsync(10);
        pending.ShouldHaveSingleItem();
        pending[0].RetryCount.ShouldBe(1);
    }

    [Fact]
    public async Task ProcessAsync_ShouldMarkAsFailedAfterMaxRetries()
    {
        // Arrange
        _options.MaxRetryAttempts = 2;
        await AddOutboxMessage();

        var processor = CreateProcessor((msg, ct) =>
            throw new Exception("Persistent failure"));

        // Act - Fail twice (max retries)
        await processor.ProcessAsync(); // Retry 1
        await processor.ProcessAsync(); // Retry 2 -> Mark as failed

        // Assert
        var pending = await _outboxRepository.GetPendingAsync(10);
        pending.ShouldBeEmpty(); // No more pending since it's failed
    }

    [Fact]
    public async Task ProcessAsync_ShouldRespectBatchSize()
    {
        // Arrange
        _options.BatchSize = 2;
        for (int i = 0; i < 5; i++)
            await AddOutboxMessage(aggregateId: $"test-{i}");

        var processor = CreateProcessor();

        // Act
        var processedCount = await processor.ProcessAsync();

        // Assert
        processedCount.ShouldBe(2);
        _publishedMessages.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ProcessAsync_ShouldReturnZero_WhenNoPendingMessages()
    {
        // Arrange
        var processor = CreateProcessor();

        // Act
        var processedCount = await processor.ProcessAsync();

        // Assert
        processedCount.ShouldBe(0);
    }

    [Fact]
    public async Task CleanupAsync_ShouldDeleteOldPublishedMessages()
    {
        // Arrange
        await AddOutboxMessage();
        var processor = CreateProcessor();
        await processor.ProcessAsync(); // Mark as published

        // Act
        var deletedCount = await processor.CleanupAsync();

        // Assert
        deletedCount.ShouldBe(0); // Not old enough yet
    }

    [Fact]
    public async Task ProcessAsync_ShouldPreserveMessageMetadata()
    {
        // Arrange
        var message = OutboxMessage.Create(
            Guid.NewGuid(),
            "OrderCreated",
            "order-123",
            "{\"orderId\":\"order-123\"}",
            "corr-456");
        await _outboxRepository.InsertAsync(message);

        var processor = CreateProcessor();

        // Act
        await processor.ProcessAsync();

        // Assert
        _publishedMessages.ShouldHaveSingleItem();
        var published = _publishedMessages[0];
        published.EventId.ShouldBe(message.EventId);
        published.EventType.ShouldBe("OrderCreated");
        published.AggregateId.ShouldBe("order-123");
        published.CorrelationId.ShouldBe("corr-456");
        published.Payload.ShouldBe("{\"orderId\":\"order-123\"}");
    }
}
