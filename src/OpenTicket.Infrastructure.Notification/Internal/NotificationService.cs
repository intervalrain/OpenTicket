using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTicket.Infrastructure.Notification.Abstractions;

namespace OpenTicket.Infrastructure.Notification.Internal;

/// <summary>
/// Default implementation of INotificationService.
/// Routes notifications to appropriate channel senders.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly IReadOnlyDictionary<NotificationChannel, INotificationSender> _senders;
    private readonly NotificationOptions _options;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IEnumerable<INotificationSender> senders,
        IOptions<NotificationOptions> options,
        ILogger<NotificationService> logger)
    {
        _senders = senders.ToDictionary(s => s.Channel);
        _options = options.Value;
        _logger = logger;
    }

    public async Task<NotificationResult> SendAsync(NotificationMessage notification, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug(
                "Notification sending is disabled. Skipping notification {NotificationId}",
                notification.Id);
            return NotificationResult.Ok(notification.Id, "disabled");
        }

        if (!_senders.TryGetValue(notification.Channel, out var sender))
        {
            _logger.LogWarning(
                "No sender registered for channel {Channel}. NotificationId: {NotificationId}",
                notification.Channel,
                notification.Id);
            return NotificationResult.Fail(notification.Id, $"No sender for channel {notification.Channel}");
        }

        return await sender.SendAsync(notification, ct);
    }

    public async Task<IReadOnlyList<NotificationResult>> SendBatchAsync(
        IEnumerable<NotificationMessage> notifications,
        CancellationToken ct = default)
    {
        var results = new List<NotificationResult>();

        foreach (var notification in notifications)
        {
            if (ct.IsCancellationRequested)
                break;

            var result = await SendAsync(notification, ct);
            results.Add(result);
        }

        return results;
    }
}
