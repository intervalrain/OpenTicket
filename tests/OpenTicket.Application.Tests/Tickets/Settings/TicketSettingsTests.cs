using OpenTicket.Application.Tickets.Settings;
using Shouldly;

namespace OpenTicket.Application.Tests.Tickets.Settings;

public class TicketSettingsTests
{
    [Fact]
    public void LockTtl_WithDefaultValue_ShouldBe120Seconds()
    {
        // Arrange
        var settings = new TicketSettings();

        // Act & Assert
        settings.LockTtlSeconds.ShouldBe(120);
        settings.LockTtl.ShouldBe(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public void LockTtl_WithCustomValue_ShouldReturnConfiguredValue()
    {
        // Arrange
        var settings = new TicketSettings { LockTtlSeconds = 300 };

        // Act & Assert
        settings.LockTtl.ShouldBe(TimeSpan.FromSeconds(300));
    }

    [Fact]
    public void DefaultLockTtlSeconds_ShouldBe120()
    {
        // Assert
        TicketSettings.DefaultLockTtlSeconds.ShouldBe(120);
    }
}
