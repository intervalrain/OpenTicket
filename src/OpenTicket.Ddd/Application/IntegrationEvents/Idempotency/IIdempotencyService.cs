namespace OpenTicket.Ddd.Application.IntegrationEvents.Idempotency;

/// <summary>
/// Service for handling idempotency of integration event processing.
/// Ensures each event is processed exactly once per consumer group.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Checks if an event has already been processed by this consumer group.
    /// </summary>
    /// <param name="eventId">The event ID to check.</param>
    /// <param name="consumerGroup">The consumer group name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the event has already been processed.</returns>
    Task<bool> HasBeenProcessedAsync(Guid eventId, string consumerGroup, CancellationToken ct = default);

    /// <summary>
    /// Marks an event as processed by this consumer group.
    /// Should be called after successful processing.
    /// </summary>
    /// <param name="eventId">The event ID that was processed.</param>
    /// <param name="eventType">The type of event.</param>
    /// <param name="consumerGroup">The consumer group name.</param>
    /// <param name="ct">Cancellation token.</param>
    Task MarkAsProcessedAsync(Guid eventId, string eventType, string consumerGroup, CancellationToken ct = default);

    /// <summary>
    /// Deletes old processed event records.
    /// Used for cleanup of the idempotency store.
    /// </summary>
    /// <param name="olderThan">Delete records older than this date.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of records deleted.</returns>
    Task<int> CleanupAsync(DateTime olderThan, CancellationToken ct = default);
}
