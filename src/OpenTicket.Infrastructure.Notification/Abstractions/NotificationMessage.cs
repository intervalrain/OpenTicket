namespace OpenTicket.Infrastructure.Notification.Abstractions;

/// <summary>
/// Represents a notification message to be sent.
/// </summary>
public class NotificationMessage
{
    /// <summary>
    /// Unique identifier for this notification.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// The recipient identifier (email, phone number, LINE ID, etc.).
    /// </summary>
    public string Recipient { get; init; } = string.Empty;

    /// <summary>
    /// The notification subject/title.
    /// </summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>
    /// The notification body/content.
    /// </summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// The channel to send through.
    /// </summary>
    public NotificationChannel Channel { get; init; }

    /// <summary>
    /// Optional template name for templated notifications.
    /// </summary>
    public string? TemplateName { get; init; }

    /// <summary>
    /// Optional template data for templated notifications.
    /// </summary>
    public IDictionary<string, object>? TemplateData { get; init; }

    /// <summary>
    /// Correlation ID for tracing.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// When the notification was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
