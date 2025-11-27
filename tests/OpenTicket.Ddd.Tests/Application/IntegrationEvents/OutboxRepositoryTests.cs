using OpenTicket.Ddd.Application.IntegrationEvents.Outbox;
using Shouldly;

namespace OpenTicket.Ddd.Tests.Application.IntegrationEvents;

public class OutboxRepositoryTests
{
    private readonly InMemoryOutboxRepository _repository;

    public OutboxRepositoryTests()
    {
        _repository = new InMemoryOutboxRepository();
    }

    private static OutboxMessage CreateMessage(string eventType = "TestEvent")
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventType = eventType,
            AggregateId = "aggregate-123",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending
        };
    }

    [Fact]
    public async Task AddAsync_ShouldAddMessage()
    {
        // Arrange
        var message = CreateMessage();

        // Act
        await _repository.AddAsync(message);
        var pending = await _repository.GetPendingAsync(10);

        // Assert
        pending.ShouldContain(m => m.Id == message.Id);
    }

    [Fact]
    public async Task AddRangeAsync_ShouldAddMultipleMessages()
    {
        // Arrange
        var messages = new[] { CreateMessage(), CreateMessage(), CreateMessage() };

        // Act
        await _repository.AddRangeAsync(messages);
        var pending = await _repository.GetPendingAsync(10);

        // Assert
        pending.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetPendingAsync_ShouldRespectBatchSize()
    {
        // Arrange
        var messages = Enumerable.Range(0, 10).Select(_ => CreateMessage()).ToList();
        await _repository.AddRangeAsync(messages);

        // Act
        var pending = await _repository.GetPendingAsync(5);

        // Assert
        pending.Count.ShouldBe(5);
    }

    [Fact]
    public async Task GetPendingAsync_ShouldMarkAsProcessing()
    {
        // Arrange
        var message = CreateMessage();
        await _repository.AddAsync(message);

        // Act
        var pending = await _repository.GetPendingAsync(10);

        // Assert
        pending.ShouldHaveSingleItem();
        pending[0].Status.ShouldBe(OutboxMessageStatus.Processing);
    }

    [Fact]
    public async Task GetPendingAsync_ShouldOrderByCreatedAt()
    {
        // Arrange
        var oldMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventType = "TestEvent",
            AggregateId = "aggregate-old",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            Status = OutboxMessageStatus.Pending
        };

        var newMessage = CreateMessage();

        await _repository.AddAsync(newMessage);
        await _repository.AddAsync(oldMessage);

        // Act
        var pending = await _repository.GetPendingAsync(10);

        // Assert
        pending.Count.ShouldBe(2);
        pending[0].CreatedAt.ShouldBeLessThan(pending[1].CreatedAt);
    }

    [Fact]
    public async Task MarkAsPublishedAsync_ShouldUpdateStatus()
    {
        // Arrange
        var message = CreateMessage();
        await _repository.AddAsync(message);
        await _repository.GetPendingAsync(10); // Mark as processing

        // Act
        await _repository.MarkAsPublishedAsync(message.Id);
        var pending = await _repository.GetPendingAsync(10);

        // Assert
        pending.ShouldBeEmpty();
    }

    [Fact]
    public async Task MarkAsFailedAsync_ShouldUpdateStatusAndError()
    {
        // Arrange
        var message = CreateMessage();
        await _repository.AddAsync(message);
        await _repository.GetPendingAsync(10); // Mark as processing

        // Act
        await _repository.MarkAsFailedAsync(message.Id, "Test error");
        var pending = await _repository.GetPendingAsync(10);

        // Assert
        pending.ShouldBeEmpty(); // Failed messages are not pending
    }

    [Fact]
    public async Task IncrementRetryAsync_ShouldIncrementCountAndResetToPending()
    {
        // Arrange
        var message = CreateMessage();
        await _repository.AddAsync(message);
        await _repository.GetPendingAsync(10); // Mark as processing

        // Act
        await _repository.IncrementRetryAsync(message.Id, "Retry error");
        var pending = await _repository.GetPendingAsync(10);

        // Assert
        pending.ShouldHaveSingleItem();
        pending[0].RetryCount.ShouldBe(1);
        pending[0].LastError.ShouldBe("Retry error");
    }

    [Fact]
    public async Task DeletePublishedAsync_ShouldDeleteOldPublishedMessages()
    {
        // Arrange
        var message = CreateMessage();
        await _repository.AddAsync(message);
        await _repository.GetPendingAsync(10);
        await _repository.MarkAsPublishedAsync(message.Id);

        // Act
        var deletedCount = await _repository.DeletePublishedAsync(DateTime.UtcNow.AddMinutes(1));

        // Assert
        deletedCount.ShouldBe(1);
    }

    [Fact]
    public async Task DeletePublishedAsync_ShouldNotDeleteRecentMessages()
    {
        // Arrange
        var message = CreateMessage();
        await _repository.AddAsync(message);
        await _repository.GetPendingAsync(10);
        await _repository.MarkAsPublishedAsync(message.Id);

        // Act
        var deletedCount = await _repository.DeletePublishedAsync(DateTime.UtcNow.AddMinutes(-1));

        // Assert
        deletedCount.ShouldBe(0);
    }
}
