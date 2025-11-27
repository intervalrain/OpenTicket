namespace OpenTicket.Infrastructure.Notification.Abstractions;

/// <summary>
/// Supported notification channels.
/// </summary>
public enum NotificationChannel
{
    /// <summary>
    /// Email notification via SMTP.
    /// </summary>
    Email = 1,

    /// <summary>
    /// SMS notification (future).
    /// </summary>
    Sms = 2,

    /// <summary>
    /// LINE messaging (future).
    /// </summary>
    Line = 3,

    /// <summary>
    /// Push notification (future).
    /// </summary>
    Push = 4
}
