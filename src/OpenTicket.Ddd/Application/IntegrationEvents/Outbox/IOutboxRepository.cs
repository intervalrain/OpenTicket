using OpenTicket.Ddd.Infrastructure;

namespace OpenTicket.Ddd.Application.IntegrationEvents.Outbox;

/// <summary>
/// Repository for managing outbox messages.
/// Extends IRepository with outbox-specific query methods.
/// </summary>
public interface IOutboxRepository : IRepository<OutboxMessage>
{
    /// <summary>
    /// Gets pending messages that are ready to be published.
    /// </summary>
    /// <param name="batchSize">Maximum number of messages to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of pending outbox messages.</returns>
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct = default);

    /// <summary>
    /// Deletes published messages older than the specified age.
    /// Used for cleanup of processed messages.
    /// </summary>
    /// <param name="olderThan">Delete messages published before this date.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of messages deleted.</returns>
    Task<int> DeletePublishedAsync(DateTime olderThan, CancellationToken ct = default);
}
