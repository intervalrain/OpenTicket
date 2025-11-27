using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenTicket.Infrastructure.Notification.Abstractions;
using OpenTicket.Infrastructure.Notification.Internal;
using Shouldly;

namespace OpenTicket.Infrastructure.Notification.Tests;

public class ConsoleNotificationSenderTests
{
    private readonly ILogger<ConsoleNotificationSender> _logger;

    public ConsoleNotificationSenderTests()
    {
        _logger = Substitute.For<ILogger<ConsoleNotificationSender>>();
    }

    [Fact]
    public async Task SendAsync_ShouldReturnSuccess()
    {
        // Arrange
        var sender = new ConsoleNotificationSender(NotificationChannel.Email, _logger);
        var notification = new NotificationMessage
        {
            Recipient = "test@example.com",
            Subject = "Test Subject",
            Body = "Test Body",
            Channel = NotificationChannel.Email,
            CorrelationId = "test-correlation"
        };

        // Act
        var result = await sender.SendAsync(notification);

        // Assert
        result.Success.ShouldBeTrue();
        result.NotificationId.ShouldBe(notification.Id);
        result.ProviderMessageId.ShouldStartWith("console-Email-");
    }

    [Fact]
    public void Channel_ShouldReturnConfiguredChannel()
    {
        // Arrange
        var sender = new ConsoleNotificationSender(NotificationChannel.Sms, _logger);

        // Act & Assert
        sender.Channel.ShouldBe(NotificationChannel.Sms);
    }

    [Fact]
    public async Task SendAsync_ShouldLogNotification()
    {
        // Arrange
        var sender = new ConsoleNotificationSender(NotificationChannel.Email, _logger);
        var notification = new NotificationMessage
        {
            Recipient = "test@example.com",
            Subject = "Test Subject",
            Body = "Test Body",
            Channel = NotificationChannel.Email
        };

        // Act
        await sender.SendAsync(notification);

        // Assert
        _logger.ReceivedWithAnyArgs(1).LogInformation(default!, default!, default!, default!, default!, default!);
    }
}
