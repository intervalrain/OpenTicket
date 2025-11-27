namespace OpenTicket.Infrastructure.Notification.Smtp;

/// <summary>
/// SMTP configuration options.
/// </summary>
public class SmtpOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Smtp";

    /// <summary>
    /// SMTP server host.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// SMTP server port.
    /// </summary>
    public int Port { get; set; } = 25;

    /// <summary>
    /// Whether to use SSL/TLS.
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// SMTP username for authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// SMTP password for authentication.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Sender email address.
    /// </summary>
    public string SenderEmail { get; set; } = "noreply@openticket.com";

    /// <summary>
    /// Sender display name.
    /// </summary>
    public string SenderName { get; set; } = "OpenTicket";

    /// <summary>
    /// Connection timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
