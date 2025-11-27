namespace OpenTicket.Infrastructure.Notification.Abstractions;

/// <summary>
/// Supported notification channels.
/// This is a flags enum allowing multiple channels to be enabled simultaneously.
/// </summary>
[Flags]
public enum NotificationChannel
{
    /// <summary>
    /// No notification channels.
    /// </summary>
    None = 0,

    /// <summary>
    /// Email notification via SMTP.
    /// </summary>
    Email = 1 << 0,

    /// <summary>
    /// SMS notification (future).
    /// </summary>
    Sms = 1 << 1,

    /// <summary>
    /// LINE messaging (future).
    /// </summary>
    Line = 1 << 2,

    /// <summary>
    /// Push notification (future).
    /// </summary>
    Push = 1 << 3
}