using Microsoft.Extensions.Logging;
using OpenTicket.Application.Contracts.Notes.Events;
using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Infrastructure.Notification.Abstractions;

namespace OpenTicket.Infrastructure.Notification.Handlers;

/// <summary>
/// Handles NoteCreatedEvent by sending email notification.
/// </summary>
public sealed class NoteCreatedEventHandler : IIntegrationEventHandler<NoteCreatedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NoteCreatedEventHandler> _logger;

    public NoteCreatedEventHandler(
        INotificationService notificationService,
        ILogger<NoteCreatedEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(NoteCreatedEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Handling NoteCreatedEvent. NoteId: {NoteId}, Title: {Title}",
            @event.NoteId,
            @event.Title);

        var notification = new NotificationMessage
        {
            Recipient = @event.NotifyEmail,
            Subject = $"[OpenTicket] Note Created: {@event.Title}",
            Body = $"""
                A new note has been created.

                Title: {@event.Title}
                Body: {@event.Body}
                Created At: {@event.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC

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
                "Notification sent for NoteCreatedEvent. NoteId: {NoteId}, MessageId: {MessageId}",
                @event.NoteId,
                result.ProviderMessageId);
        }
        else
        {
            _logger.LogWarning(
                "Failed to send notification for NoteCreatedEvent. NoteId: {NoteId}, Error: {Error}",
                @event.NoteId,
                result.ErrorMessage);
        }
    }
}
