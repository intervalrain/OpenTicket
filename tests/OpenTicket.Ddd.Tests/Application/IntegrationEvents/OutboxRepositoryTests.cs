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
        return OutboxMessage.Create(
            Guid.NewGuid(),
            eventType,
            "aggregate-123",
            "{}");
    }

    private static OutboxMessage CreateMessageWithTime(DateTime createdAt)
    {
        var message = OutboxMessage.Create(
            Guid.NewGuid(),
            "TestEvent",
            "aggregate-123",
            "{}");

        // Use reflection to set CreatedAt for testing ordering
        typeof(OutboxMessage)
            .GetProperty(nameof(OutboxMessage.CreatedAt))!
            .SetValue(message, createdAt);

        return message;
    }

    [Fact]
    public async Task InsertAsync_ShouldAddMessage()
    {
        // Arrange
        var message = CreateMessage();

        // Act
        await _repository.InsertAsync(message);
        var pending = await _repository.GetPendingAsync(10);

        // Assert
        pending.ShouldContain(m => m.Id == message.Id);
    }

    [Fact]
    public async Task InsertAsync_ShouldAddMultipleMessages()
    {
        // Arrange
        var messages = new[] { CreateMessage(), CreateMessage(), CreateMessage() };

        // Act
        foreach (var message in messages)
        {
            await _repository.InsertAsync(message);
        }
        var pending = await _repository.GetPendingAsync(10);

        // Assert
        pending.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetPendingAsync_ShouldRespectBatchSize()
    {
        // Arrange
        var messages = Enumerable.Range(0, 10).Select(_ => CreateMessage()).ToList();
        foreach (var message in messages)
        {
            await _repository.InsertAsync(message);
        }

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
        await _repository.InsertAsync(message);

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
        var oldMessage = CreateMessageWithTime(DateTime.UtcNow.AddMinutes(-5));
        var newMessage = CreateMessage();

        await _repository.InsertAsync(newMessage);
        await _repository.InsertAsync(oldMessage);

        // Act
        var pending = await _repository.GetPendingAsync(10);

        // Assert
        pending.Count.ShouldBe(2);
        pending[0].CreatedAt.ShouldBeLessThan(pending[1].CreatedAt);
    }

    [Fact]
    public async Task MarkAsPublished_ShouldUpdateStatus()
    {
        // Arrange
        var message = CreateMessage();
        await _repository.InsertAsync(message);
        await _repository.GetPendingAsync(10); // Mark as processing

        // Act
        message.MarkAsPublished();
        await _repository.UpdateAsync(message);
        var pending = await _repository.GetPendingAsync(10);

        // Assert
        pending.ShouldBeEmpty();
    }

    [Fact]
    public async Task MarkAsFailed_ShouldUpdateStatusAndError()
    {
        // Arrange
        var message = CreateMessage();
        await _repository.InsertAsync(message);
        await _repository.GetPendingAsync(10); // Mark as processing

        // Act
        message.MarkAsFailed("Test error");
        await _repository.UpdateAsync(message);
        var pending = await _repository.GetPendingAsync(10);

        // Assert
        pending.ShouldBeEmpty(); // Failed messages are not pending
    }

    [Fact]
    public async Task IncrementRetry_ShouldIncrementCountAndResetToPending()
    {
        // Arrange
        var message = CreateMessage();
        await _repository.InsertAsync(message);
        await _repository.GetPendingAsync(10); // Mark as processing

        // Act
        message.IncrementRetry("Retry error");
        await _repository.UpdateAsync(message);
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
        await _repository.InsertAsync(message);
        await _repository.GetPendingAsync(10);
        message.MarkAsPublished();
        await _repository.UpdateAsync(message);

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
        await _repository.InsertAsync(message);
        await _repository.GetPendingAsync(10);
        message.MarkAsPublished();
        await _repository.UpdateAsync(message);

        // Act
        var deletedCount = await _repository.DeletePublishedAsync(DateTime.UtcNow.AddMinutes(-1));

        // Assert
        deletedCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnMessage()
    {
        // Arrange
        var message = CreateMessage();
        await _repository.InsertAsync(message);

        // Act
        var result = await _repository.GetAsync(message.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(message.Id);
    }

    [Fact]
    public async Task GetAsync_ShouldThrowWhenNotFound()
    {
        // Act & Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () =>
            await _repository.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task FindAsync_ShouldReturnNullWhenNotFound()
    {
        // Act
        var result = await _repository.FindAsync(Guid.NewGuid());

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveMessage()
    {
        // Arrange
        var message = CreateMessage();
        await _repository.InsertAsync(message);

        // Act
        await _repository.DeleteAsync(message);
        var result = await _repository.FindAsync(message.Id);

        // Assert
        result.ShouldBeNull();
    }
}
