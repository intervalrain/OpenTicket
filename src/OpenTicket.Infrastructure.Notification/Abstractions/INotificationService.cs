namespace OpenTicket.Infrastructure.Notification.Abstractions;

/// <summary>
/// High-level notification service that routes notifications to appropriate channels.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a notification through the specified channel.
    /// </summary>
    /// <param name="notification">The notification to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the send operation.</returns>
    Task<NotificationResult> SendAsync(NotificationMessage notification, CancellationToken ct = default);

    /// <summary>
    /// Sends a notification to multiple recipients.
    /// </summary>
    /// <param name="notifications">The notifications to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Results for each notification.</returns>
    Task<IReadOnlyList<NotificationResult>> SendBatchAsync(
        IEnumerable<NotificationMessage> notifications,
        CancellationToken ct = default);
}
