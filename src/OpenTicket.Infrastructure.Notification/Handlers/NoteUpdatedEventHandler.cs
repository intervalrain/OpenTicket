using Microsoft.Extensions.Logging;
using OpenTicket.Application.Contracts.Notes.Events;
using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Infrastructure.Notification.Abstractions;

namespace OpenTicket.Infrastructure.Notification.Handlers;

/// <summary>
/// Handles NoteUpdatedEvent by sending email notification.
/// </summary>
public sealed class NoteUpdatedEventHandler : IIntegrationEventHandler<NoteUpdatedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NoteUpdatedEventHandler> _logger;

    public NoteUpdatedEventHandler(
        INotificationService notificationService,
        ILogger<NoteUpdatedEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(NoteUpdatedEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Handling NoteUpdatedEvent. NoteId: {NoteId}, NewTitle: {NewTitle}",
            @event.NoteId,
            @event.NewTitle);

        var notification = new NotificationMessage
        {
            Recipient = @event.NotifyEmail,
            Subject = $"[OpenTicket] Note Updated: {@event.NewTitle}",
            Body = $"""
                A note has been updated.

                Previous Title: {@event.PreviousTitle}
                New Title: {@event.NewTitle}

                Previous Body: {@event.PreviousBody}
                New Body: {@event.NewBody}

                Updated At: {@event.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC

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
                "Notification sent for NoteUpdatedEvent. NoteId: {NoteId}, MessageId: {MessageId}",
                @event.NoteId,
                result.ProviderMessageId);
        }
        else
        {
            _logger.LogWarning(
                "Failed to send notification for NoteUpdatedEvent. NoteId: {NoteId}, Error: {Error}",
                @event.NoteId,
                result.ErrorMessage);
        }
    }
}
