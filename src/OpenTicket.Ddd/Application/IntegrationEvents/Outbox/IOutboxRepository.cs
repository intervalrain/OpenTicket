namespace OpenTicket.Ddd.Application.IntegrationEvents.Outbox;

/// <summary>
/// Repository for managing outbox messages.
/// </summary>
public interface IOutboxRepository
{
    /// <summary>
    /// Adds a message to the outbox.
    /// Should be called within the same transaction as the domain operation.
    /// </summary>
    /// <param name="message">The outbox message to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);

    /// <summary>
    /// Adds multiple messages to the outbox.
    /// Should be called within the same transaction as the domain operation.
    /// </summary>
    /// <param name="messages">The outbox messages to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddRangeAsync(IEnumerable<OutboxMessage> messages, CancellationToken ct = default);

    /// <summary>
    /// Gets pending messages that are ready to be published.
    /// </summary>
    /// <param name="batchSize">Maximum number of messages to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of pending outbox messages.</returns>
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct = default);

    /// <summary>
    /// Marks a message as published.
    /// </summary>
    /// <param name="id">The outbox message ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task MarkAsPublishedAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Marks a message as failed.
    /// </summary>
    /// <param name="id">The outbox message ID.</param>
    /// <param name="error">The error message.</param>
    /// <param name="ct">Cancellation token.</param>
    Task MarkAsFailedAsync(Guid id, string error, CancellationToken ct = default);

    /// <summary>
    /// Increments the retry count for a message.
    /// </summary>
    /// <param name="id">The outbox message ID.</param>
    /// <param name="error">The error message from the last attempt.</param>
    /// <param name="ct">Cancellation token.</param>
    Task IncrementRetryAsync(Guid id, string error, CancellationToken ct = default);

    /// <summary>
    /// Deletes published messages older than the specified age.
    /// Used for cleanup of processed messages.
    /// </summary>
    /// <param name="olderThan">Delete messages published before this date.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of messages deleted.</returns>
    Task<int> DeletePublishedAsync(DateTime olderThan, CancellationToken ct = default);
}
