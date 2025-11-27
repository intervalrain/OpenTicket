namespace OpenTicket.Infrastructure.Notification.Abstractions;

/// <summary>
/// Configuration options for the notification service.
/// </summary>
public class NotificationOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Notification";

    /// <summary>
    /// Default sender name.
    /// </summary>
    public string DefaultSenderName { get; set; } = "OpenTicket";

    /// <summary>
    /// Default sender email.
    /// </summary>
    public string DefaultSenderEmail { get; set; } = "noreply@openticket.com";

    /// <summary>
    /// Whether to enable notification sending.
    /// Set to false for development/testing.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum retry attempts for failed notifications.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
}
