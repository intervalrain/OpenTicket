namespace OpenTicket.Ddd.Application.IntegrationEvents.Outbox;

/// <summary>
/// Processes outbox messages and publishes them to the message broker.
/// </summary>
public interface IOutboxProcessor
{
    /// <summary>
    /// Processes pending outbox messages.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of messages processed.</returns>
    Task<int> ProcessAsync(CancellationToken ct = default);

    /// <summary>
    /// Cleans up old published messages.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of messages deleted.</returns>
    Task<int> CleanupAsync(CancellationToken ct = default);
}
