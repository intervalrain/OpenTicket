using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Ddd.Application.IntegrationEvents.Internal;
using Shouldly;

namespace OpenTicket.Ddd.Tests.Application.IntegrationEvents;

public class IntegrationEventSerializerTests
{
    private record TestEvent : IntegrationEvent
    {
        public string OrderId { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public DateTime EventTime { get; init; }
        public override string AggregateId => OrderId;
    }

    [Fact]
    public void Serialize_ShouldProduceValidJson()
    {
        // Arrange
        var @event = new TestEvent
        {
            OrderId = "order-123",
            Amount = 99.99m,
            EventTime = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };

        // Act
        var json = IntegrationEventSerializer.Serialize(@event);

        // Assert
        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("orderId");
        json.ShouldContain("order-123");
        json.ShouldContain("99.99");
    }

    [Fact]
    public void Serialize_ShouldUseCamelCase()
    {
        // Arrange
        var @event = new TestEvent { OrderId = "test" };

        // Act
        var json = IntegrationEventSerializer.Serialize(@event);

        // Assert
        json.ShouldContain("orderId");
        // Verify camelCase is used (starts with lowercase 'o')
        json.ShouldContain("\"orderId\"");
    }

    [Fact]
    public void Deserialize_ShouldRestoreEvent()
    {
        // Arrange
        var original = new TestEvent
        {
            OrderId = "order-456",
            Amount = 150.00m,
            EventTime = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };
        var json = IntegrationEventSerializer.Serialize(original);

        // Act
        var restored = IntegrationEventSerializer.Deserialize<TestEvent>(json);

        // Assert
        restored.ShouldNotBeNull();
        restored.OrderId.ShouldBe("order-456");
        restored.Amount.ShouldBe(150.00m);
    }

    [Fact]
    public void Deserialize_WithType_ShouldRestoreEvent()
    {
        // Arrange
        var original = new TestEvent { OrderId = "order-789" };
        var json = IntegrationEventSerializer.Serialize(original);

        // Act
        var restored = IntegrationEventSerializer.Deserialize(json, typeof(TestEvent)) as TestEvent;

        // Assert
        restored.ShouldNotBeNull();
        restored.OrderId.ShouldBe("order-789");
    }

    [Fact]
    public void RoundTrip_ShouldPreserveAllProperties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var original = new TestEvent
        {
            OrderId = "order-roundtrip",
            Amount = 999.99m,
            EventTime = DateTime.UtcNow,
            CorrelationId = correlationId
        };

        // Act
        var json = IntegrationEventSerializer.Serialize(original);
        var restored = IntegrationEventSerializer.Deserialize<TestEvent>(json);

        // Assert
        restored.ShouldNotBeNull();
        restored.OrderId.ShouldBe(original.OrderId);
        restored.Amount.ShouldBe(original.Amount);
        restored.CorrelationId.ShouldBe(correlationId);
        restored.EventId.ShouldBe(original.EventId);
    }
}
