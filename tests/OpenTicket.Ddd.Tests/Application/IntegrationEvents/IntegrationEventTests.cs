using OpenTicket.Ddd.Application.IntegrationEvents;
using Shouldly;

namespace OpenTicket.Ddd.Tests.Application.IntegrationEvents;

public class IntegrationEventTests
{
    // Test event for testing purposes
    private record TestEvent : IntegrationEvent
    {
        public string TestData { get; init; } = string.Empty;
        public override string AggregateId => TestData;
    }

    [Fact]
    public void IntegrationEvent_ShouldHaveUniqueEventId()
    {
        // Arrange & Act
        var event1 = new TestEvent { TestData = "test1" };
        var event2 = new TestEvent { TestData = "test2" };

        // Assert
        event1.EventId.ShouldNotBe(Guid.Empty);
        event2.EventId.ShouldNotBe(Guid.Empty);
        event1.EventId.ShouldNotBe(event2.EventId);
    }

    [Fact]
    public void IntegrationEvent_ShouldHaveOccurredAt()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var @event = new TestEvent { TestData = "test" };
        var after = DateTime.UtcNow;

        // Assert
        @event.OccurredAt.ShouldBeGreaterThanOrEqualTo(before);
        @event.OccurredAt.ShouldBeLessThanOrEqualTo(after);
    }

    [Fact]
    public void IntegrationEvent_ShouldReturnTypeNameAsEventType()
    {
        // Arrange & Act
        var @event = new TestEvent { TestData = "test" };

        // Assert
        @event.EventType.ShouldBe("TestEvent");
    }

    [Fact]
    public void IntegrationEvent_ShouldPreserveCorrelationId()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var @event = new TestEvent
        {
            TestData = "test",
            CorrelationId = correlationId
        };

        // Assert
        @event.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public void IntegrationEvent_ShouldReturnAggregateId()
    {
        // Arrange
        var aggregateId = "aggregate-123";

        // Act
        var @event = new TestEvent { TestData = aggregateId };

        // Assert
        @event.AggregateId.ShouldBe(aggregateId);
    }
}
