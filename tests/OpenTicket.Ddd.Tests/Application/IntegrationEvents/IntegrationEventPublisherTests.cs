using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Ddd.Application.IntegrationEvents.Internal;
using OpenTicket.Ddd.Application.IntegrationEvents.Outbox;
using Shouldly;

namespace OpenTicket.Ddd.Tests.Application.IntegrationEvents;

public class IntegrationEventPublisherTests
{
    private record TestEvent : IntegrationEvent
    {
        public string OrderId { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public override string AggregateId => OrderId;
    }

    private readonly InMemoryOutboxRepository _outboxRepository;
    private readonly OutboxIntegrationEventPublisher _publisher;

    public IntegrationEventPublisherTests()
    {
        _outboxRepository = new InMemoryOutboxRepository();
        _publisher = new OutboxIntegrationEventPublisher(_outboxRepository);
    }

    [Fact]
    public async Task PublishAsync_ShouldAddMessageToOutbox()
    {
        // Arrange
        var @event = new TestEvent
        {
            OrderId = "order-123",
            Status = "Created"
        };

        // Act
        await _publisher.PublishAsync(@event);
        var pending = await _outboxRepository.GetPendingAsync(10);

        // Assert
        pending.ShouldHaveSingleItem();
        pending[0].EventId.ShouldBe(@event.EventId);
        pending[0].EventType.ShouldBe("TestEvent");
        pending[0].AggregateId.ShouldBe("order-123");
    }

    [Fact]
    public async Task PublishAsync_ShouldSerializeEventPayload()
    {
        // Arrange
        var @event = new TestEvent
        {
            OrderId = "order-123",
            Status = "Created"
        };

        // Act
        await _publisher.PublishAsync(@event);
        var pending = await _outboxRepository.GetPendingAsync(10);

        // Assert
        pending[0].Payload.ShouldContain("order-123");
        pending[0].Payload.ShouldContain("Created");
    }

    [Fact]
    public async Task PublishAsync_ShouldPreserveCorrelationId()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var @event = new TestEvent
        {
            OrderId = "order-123",
            Status = "Created",
            CorrelationId = correlationId
        };

        // Act
        await _publisher.PublishAsync(@event);
        var pending = await _outboxRepository.GetPendingAsync(10);

        // Assert
        pending[0].CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public async Task PublishAsync_Batch_ShouldAddAllMessagesToOutbox()
    {
        // Arrange
        var events = new[]
        {
            new TestEvent { OrderId = "order-1", Status = "Created" },
            new TestEvent { OrderId = "order-2", Status = "Created" },
            new TestEvent { OrderId = "order-3", Status = "Created" }
        };

        // Act
        await _publisher.PublishAsync(events);
        var pending = await _outboxRepository.GetPendingAsync(10);

        // Assert
        pending.Count.ShouldBe(3);
    }

    [Fact]
    public async Task PublishAsync_ShouldSetPendingStatus()
    {
        // Arrange
        var @event = new TestEvent
        {
            OrderId = "order-123",
            Status = "Created"
        };

        // Act
        await _publisher.PublishAsync(@event);
        var pending = await _outboxRepository.GetPendingAsync(10);

        // Assert
        // After GetPendingAsync, status should be Processing
        pending[0].Status.ShouldBe(OutboxMessageStatus.Processing);
    }
}
