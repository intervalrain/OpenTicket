using Microsoft.Extensions.Logging;
using OpenTicket.Application.Contracts.Notes.Events;
using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Infrastructure.Notification.Abstractions;

namespace OpenTicket.Infrastructure.Notification.Handlers;

/// <summary>
/// Handles NotePatchedEvent by sending email notification.
/// </summary>
public sealed class NotePatchedEventHandler : IIntegrationEventHandler<NotePatchedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotePatchedEventHandler> _logger;

    public NotePatchedEventHandler(
        INotificationService notificationService,
        ILogger<NotePatchedEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(NotePatchedEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Handling NotePatchedEvent. NoteId: {NoteId}, PatchedFields: {PatchedFields}",
            @event.NoteId,
            string.Join(", ", @event.PatchedFields));

        var changesDescription = BuildChangesDescription(@event);

        var notification = new NotificationMessage
        {
            Recipient = @event.NotifyEmail,
            Subject = $"[OpenTicket] Note Patched: {@event.NoteId}",
            Body = $"""
                A note has been partially updated.

                Note ID: {@event.NoteId}
                Patched Fields: {string.Join(", ", @event.PatchedFields)}

                Changes:
                {changesDescription}

                Patched At: {@event.PatchedAt:yyyy-MM-dd HH:mm:ss} UTC

                --
                OpenTicket Notification System
                """,
            Channel = NotificationChannel.Email,
            CorrelationId = @event.CorrelationId
        };

        var result = await _notificationService.SendAsync(notification, ct);

        if (result.Success)
        {
            _logger.LogInformation(
                "Notification sent for NotePatchedEvent. NoteId: {NoteId}, MessageId: {MessageId}",
                @event.NoteId,
                result.ProviderMessageId);
        }
        else
        {
            _logger.LogWarning(
                "Failed to send notification for NotePatchedEvent. NoteId: {NoteId}, Error: {Error}",
                @event.NoteId,
                result.ErrorMessage);
        }
    }

    private static string BuildChangesDescription(NotePatchedEvent @event)
    {
        var changes = new List<string>();

        if (@event.NewTitle is not null)
            changes.Add($"- Title: {@event.NewTitle}");

        if (@event.NewBody is not null)
            changes.Add($"- Body: {@event.NewBody}");

        return changes.Count > 0 ? string.Join("\n", changes) : "No changes recorded";
    }
}
