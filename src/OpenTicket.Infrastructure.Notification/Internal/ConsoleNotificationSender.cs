using Microsoft.Extensions.Logging;
using OpenTicket.Infrastructure.Notification.Abstractions;

namespace OpenTicket.Infrastructure.Notification.Internal;

/// <summary>
/// Console-based notification sender for testing and MVP mode.
/// Logs notifications instead of actually sending them.
/// </summary>
public sealed class ConsoleNotificationSender : INotificationSender
{
    private readonly ILogger<ConsoleNotificationSender> _logger;
    private readonly NotificationChannel _channel;

    public ConsoleNotificationSender(
        NotificationChannel channel,
        ILogger<ConsoleNotificationSender> logger)
    {
        _channel = channel;
        _logger = logger;
    }

    public NotificationChannel Channel => _channel;

    public Task<NotificationResult> SendAsync(NotificationMessage notification, CancellationToken ct = default)
    {
        _logger.LogInformation(
            """
            [CONSOLE {Channel}] Notification sent:
            To: {Recipient}
            Subject: {Subject}
            Body: {Body}
            CorrelationId: {CorrelationId}
            """,
            _channel,
            notification.Recipient,
            notification.Subject,
            notification.Body,
            notification.CorrelationId ?? "N/A");

        return Task.FromResult(NotificationResult.Ok(
            notification.Id,
            $"console-{_channel}-{Guid.NewGuid():N}"));
    }
}
