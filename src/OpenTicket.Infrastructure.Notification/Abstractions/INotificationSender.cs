namespace OpenTicket.Infrastructure.Notification.Abstractions;

/// <summary>
/// Sends notifications through a specific channel.
/// Each channel (Email, SMS, LINE, etc.) implements this interface.
/// </summary>
public interface INotificationSender
{
    /// <summary>
    /// The channel this sender handles.
    /// </summary>
    NotificationChannel Channel { get; }

    /// <summary>
    /// Sends a notification.
    /// </summary>
    /// <param name="notification">The notification to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the send operation.</returns>
    Task<NotificationResult> SendAsync(NotificationMessage notification, CancellationToken ct = default);
}
