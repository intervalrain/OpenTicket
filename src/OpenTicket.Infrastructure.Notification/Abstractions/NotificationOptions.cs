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

    /// <summary>
    /// Notification mode: Console for testing/development, Production for real sending.
    /// </summary>
    public NotificationMode Mode { get; set; } = NotificationMode.Console;
}

/// <summary>
/// Notification sending mode.
/// </summary>
public enum NotificationMode
{
    /// <summary>
    /// Console mode: logs notifications instead of sending them.
    /// Useful for development and testing.
    /// </summary>
    Console,

    /// <summary>
    /// Production mode: sends notifications via real providers (SMTP, SMS gateway, etc.).
    /// </summary>
    Production
}