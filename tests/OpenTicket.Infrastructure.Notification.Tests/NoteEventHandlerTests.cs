using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenTicket.Application.Contracts.Notes.Events;
using OpenTicket.Infrastructure.Notification.Abstractions;
using OpenTicket.Infrastructure.Notification.Handlers;
using Shouldly;

namespace OpenTicket.Infrastructure.Notification.Tests;

public class NoteEventHandlerTests
{
    private readonly INotificationService _notificationService;
    private NotificationMessage? _capturedNotification;

    public NoteEventHandlerTests()
    {
        _notificationService = Substitute.For<INotificationService>();
        _notificationService.SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                _capturedNotification = ci.Arg<NotificationMessage>();
                return NotificationResult.Ok(_capturedNotification.Id, "test-message-id");
            });
    }

    [Fact]
    public async Task NoteCreatedEventHandler_ShouldSendEmailNotification()
    {
        // Arrange
        var logger = Substitute.For<ILogger<NoteCreatedEventHandler>>();
        var handler = new NoteCreatedEventHandler(_notificationService, logger);

        var @event = new NoteCreatedEvent
        {
            NoteId = Guid.NewGuid(),
            Title = "Test Note",
            Body = "This is a test note body",
            CreatedAt = DateTime.UtcNow,
            NotifyEmail = "user@example.com",
            CorrelationId = "corr-123"
        };

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _capturedNotification.ShouldNotBeNull();
        _capturedNotification.Recipient.ShouldBe("user@example.com");
        _capturedNotification.Subject.ShouldContain("Note Created");
        _capturedNotification.Subject.ShouldContain("Test Note");
        _capturedNotification.Body.ShouldContain("Test Note");
        _capturedNotification.Body.ShouldContain("This is a test note body");
        _capturedNotification.Channel.ShouldBe(NotificationChannel.Email);
        _capturedNotification.CorrelationId.ShouldBe("corr-123");
    }

    [Fact]
    public async Task NoteUpdatedEventHandler_ShouldSendEmailNotification()
    {
        // Arrange
        var logger = Substitute.For<ILogger<NoteUpdatedEventHandler>>();
        var handler = new NoteUpdatedEventHandler(_notificationService, logger);

        var @event = new NoteUpdatedEvent
        {
            NoteId = Guid.NewGuid(),
            PreviousTitle = "Old Title",
            NewTitle = "New Title",
            PreviousBody = "Old body content",
            NewBody = "New body content",
            UpdatedAt = DateTime.UtcNow,
            NotifyEmail = "user@example.com"
        };

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _capturedNotification.ShouldNotBeNull();
        _capturedNotification.Recipient.ShouldBe("user@example.com");
        _capturedNotification.Subject.ShouldContain("Note Updated");
        _capturedNotification.Body.ShouldContain("Old Title");
        _capturedNotification.Body.ShouldContain("New Title");
        _capturedNotification.Body.ShouldContain("Old body content");
        _capturedNotification.Body.ShouldContain("New body content");
    }

    [Fact]
    public async Task NotePatchedEventHandler_ShouldSendEmailNotification()
    {
        // Arrange
        var logger = Substitute.For<ILogger<NotePatchedEventHandler>>();
        var handler = new NotePatchedEventHandler(_notificationService, logger);

        var @event = new NotePatchedEvent
        {
            NoteId = Guid.NewGuid(),
            PatchedFields = ["Title", "Body"],
            NewTitle = "Patched Title",
            NewBody = "Patched body",
            PatchedAt = DateTime.UtcNow,
            NotifyEmail = "user@example.com"
        };

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _capturedNotification.ShouldNotBeNull();
        _capturedNotification.Recipient.ShouldBe("user@example.com");
        _capturedNotification.Subject.ShouldContain("Note Patched");
        _capturedNotification.Body.ShouldContain("Title, Body");
        _capturedNotification.Body.ShouldContain("Patched Title");
        _capturedNotification.Body.ShouldContain("Patched body");
    }

    [Fact]
    public async Task NotePatchedEventHandler_WithOnlyTitlePatched_ShouldShowOnlyTitle()
    {
        // Arrange
        var logger = Substitute.For<ILogger<NotePatchedEventHandler>>();
        var handler = new NotePatchedEventHandler(_notificationService, logger);

        var @event = new NotePatchedEvent
        {
            NoteId = Guid.NewGuid(),
            PatchedFields = ["Title"],
            NewTitle = "Only Title Changed",
            NewBody = null,
            PatchedAt = DateTime.UtcNow,
            NotifyEmail = "user@example.com"
        };

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _capturedNotification.ShouldNotBeNull();
        _capturedNotification.Body.ShouldContain("Only Title Changed");
    }
}
