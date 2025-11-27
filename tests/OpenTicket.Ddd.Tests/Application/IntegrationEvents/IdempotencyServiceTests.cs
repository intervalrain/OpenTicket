using OpenTicket.Ddd.Application.IntegrationEvents.Idempotency;
using Shouldly;

namespace OpenTicket.Ddd.Tests.Application.IntegrationEvents;

public class IdempotencyServiceTests
{
    private readonly InMemoryIdempotencyService _service;

    public IdempotencyServiceTests()
    {
        _service = new InMemoryIdempotencyService();
    }

    [Fact]
    public async Task HasBeenProcessedAsync_ShouldReturnFalse_WhenNotProcessed()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var consumerGroup = "test-group";

        // Act
        var result = await _service.HasBeenProcessedAsync(eventId, consumerGroup);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task HasBeenProcessedAsync_ShouldReturnTrue_WhenProcessed()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var consumerGroup = "test-group";
        await _service.MarkAsProcessedAsync(eventId, "TestEvent", consumerGroup);

        // Act
        var result = await _service.HasBeenProcessedAsync(eventId, consumerGroup);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task HasBeenProcessedAsync_ShouldReturnFalse_ForDifferentConsumerGroup()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        await _service.MarkAsProcessedAsync(eventId, "TestEvent", "group-1");

        // Act
        var result = await _service.HasBeenProcessedAsync(eventId, "group-2");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task MarkAsProcessedAsync_ShouldBeIdempotent()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var consumerGroup = "test-group";

        // Act - Mark twice
        await _service.MarkAsProcessedAsync(eventId, "TestEvent", consumerGroup);
        await _service.MarkAsProcessedAsync(eventId, "TestEvent", consumerGroup);

        var result = await _service.HasBeenProcessedAsync(eventId, consumerGroup);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task CleanupAsync_ShouldDeleteOldRecords()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var consumerGroup = "test-group";
        await _service.MarkAsProcessedAsync(eventId, "TestEvent", consumerGroup);

        // Act
        var deletedCount = await _service.CleanupAsync(DateTime.UtcNow.AddMinutes(1));

        // Assert
        deletedCount.ShouldBe(1);

        var result = await _service.HasBeenProcessedAsync(eventId, consumerGroup);
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task CleanupAsync_ShouldNotDeleteRecentRecords()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var consumerGroup = "test-group";
        await _service.MarkAsProcessedAsync(eventId, "TestEvent", consumerGroup);

        // Act
        var deletedCount = await _service.CleanupAsync(DateTime.UtcNow.AddMinutes(-1));

        // Assert
        deletedCount.ShouldBe(0);

        var result = await _service.HasBeenProcessedAsync(eventId, consumerGroup);
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task SameEventId_DifferentConsumerGroups_ShouldBothBeTracked()
    {
        // Arrange
        var eventId = Guid.NewGuid();

        // Act
        await _service.MarkAsProcessedAsync(eventId, "TestEvent", "group-1");
        await _service.MarkAsProcessedAsync(eventId, "TestEvent", "group-2");

        // Assert
        (await _service.HasBeenProcessedAsync(eventId, "group-1")).ShouldBeTrue();
        (await _service.HasBeenProcessedAsync(eventId, "group-2")).ShouldBeTrue();
        (await _service.HasBeenProcessedAsync(eventId, "group-3")).ShouldBeFalse();
    }
}
