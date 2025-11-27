using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using OpenTicket.Infrastructure.Notification.Abstractions;
using OpenTicket.Infrastructure.Notification.Internal;
using Shouldly;

namespace OpenTicket.Infrastructure.Notification.Tests;

public class NotificationServiceTests
{
    private readonly ILogger<NotificationService> _logger;
    private readonly NotificationOptions _options;

    public NotificationServiceTests()
    {
        _logger = Substitute.For<ILogger<NotificationService>>();
        _options = new NotificationOptions { Enabled = true };
    }

    private NotificationService CreateService(params INotificationSender[] senders)
    {
        return new NotificationService(
            senders,
            Options.Create(_options),
            _logger);
    }

    [Fact]
    public async Task SendAsync_WithRegisteredChannel_ShouldCallSender()
    {
        // Arrange
        var sender = Substitute.For<INotificationSender>();
        sender.Channel.Returns(NotificationChannel.Email);
        sender.SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>())
            .Returns(NotificationResult.Ok(Guid.NewGuid(), "test-message-id"));

        var service = CreateService(sender);
        var notification = new NotificationMessage
        {
            Recipient = "test@example.com",
            Subject = "Test",
            Body = "Test body",
            Channel = NotificationChannel.Email
        };

        // Act
        var result = await service.SendAsync(notification);

        // Assert
        result.Success.ShouldBeTrue();
        await sender.Received(1).SendAsync(notification, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithUnregisteredChannel_ShouldReturnFailure()
    {
        // Arrange
        var service = CreateService(); // No senders registered
        var notification = new NotificationMessage
        {
            Recipient = "test@example.com",
            Subject = "Test",
            Body = "Test body",
            Channel = NotificationChannel.Email
        };

        // Act
        var result = await service.SendAsync(notification);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("No sender for channel");
    }

    [Fact]
    public async Task SendAsync_WhenDisabled_ShouldReturnSuccess()
    {
        // Arrange
        _options.Enabled = false;
        var sender = Substitute.For<INotificationSender>();
        sender.Channel.Returns(NotificationChannel.Email);

        var service = CreateService(sender);
        var notification = new NotificationMessage
        {
            Recipient = "test@example.com",
            Subject = "Test",
            Body = "Test body",
            Channel = NotificationChannel.Email
        };

        // Act
        var result = await service.SendAsync(notification);

        // Assert
        result.Success.ShouldBeTrue();
        result.ProviderMessageId.ShouldBe("disabled");
        await sender.DidNotReceive().SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendBatchAsync_ShouldSendAllNotifications()
    {
        // Arrange
        var sender = Substitute.For<INotificationSender>();
        sender.Channel.Returns(NotificationChannel.Email);
        sender.SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>())
            .Returns(ci => NotificationResult.Ok(ci.Arg<NotificationMessage>().Id, "test"));

        var service = CreateService(sender);
        var notifications = new[]
        {
            new NotificationMessage { Recipient = "a@test.com", Subject = "1", Body = "1", Channel = NotificationChannel.Email },
            new NotificationMessage { Recipient = "b@test.com", Subject = "2", Body = "2", Channel = NotificationChannel.Email },
            new NotificationMessage { Recipient = "c@test.com", Subject = "3", Body = "3", Channel = NotificationChannel.Email }
        };

        // Act
        var results = await service.SendBatchAsync(notifications);

        // Assert
        results.Count.ShouldBe(3);
        results.ShouldAllBe(r => r.Success);
        await sender.Received(3).SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }
}
