namespace OpenTicket.Infrastructure.Notification.Abstractions;

/// <summary>
/// Result of sending a notification.
/// </summary>
public record NotificationResult
{
    /// <summary>
    /// Whether the notification was sent successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The notification ID.
    /// </summary>
    public Guid NotificationId { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Provider-specific message ID (e.g., SMTP message ID).
    /// </summary>
    public string? ProviderMessageId { get; init; }

    /// <summary>
    /// When the notification was sent.
    /// </summary>
    public DateTime? SentAt { get; init; }

    public static NotificationResult Ok(Guid notificationId, string? providerMessageId = null)
        => new()
        {
            Success = true,
            NotificationId = notificationId,
            ProviderMessageId = providerMessageId,
            SentAt = DateTime.UtcNow
        };

    public static NotificationResult Fail(Guid notificationId, string errorMessage)
        => new()
        {
            Success = false,
            NotificationId = notificationId,
            ErrorMessage = errorMessage
        };
}
