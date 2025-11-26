using OpenTicket.Domain.Shared.Identities;
using OpenTicket.Domain.Shared.Tickets.Enums;
using OpenTicket.Domain.Shared.Tickets.Events;
using OpenTicket.Domain.Tickets.Entities;
using Shouldly;

namespace OpenTicket.Domain.Tests.Tickets;

public class SeatTests
{
    private readonly SessionId _sessionId = SessionId.New();
    private readonly AreaId _areaId = AreaId.New();
    private readonly UserId _userId = UserId.New();
    private const string SeatNumber = "A-001";

    private Seat CreateSeat() => Seat.Create(_sessionId, _areaId, SeatNumber);

    [Fact]
    public void Create_ShouldReturnSeat_WithAvailableStatus()
    {
        // Act
        var seat = CreateSeat();

        // Assert
        seat.SessionId.ShouldBe(_sessionId);
        seat.AreaId.ShouldBe(_areaId);
        seat.Number.ShouldBe(SeatNumber);
        seat.Status.ShouldBe(SeatStatus.Available);
        seat.LockedBy.ShouldBeNull();
        seat.LockExpiresAt.ShouldBeNull();
    }

    [Fact]
    public void Lock_WhenAvailable_ShouldSucceed()
    {
        // Arrange
        var seat = CreateSeat();
        var ttl = TimeSpan.FromMinutes(2);

        // Act
        var result = seat.Lock(_userId, ttl);

        // Assert
        result.IsError.ShouldBeFalse();
        seat.Status.ShouldBe(SeatStatus.Locked);
        seat.LockedBy.ShouldBe(_userId);
        seat.LockExpiresAt.ShouldNotBeNull();
    }

    [Fact]
    public void Lock_WhenAvailable_ShouldRaiseSeatLockedEvent()
    {
        // Arrange
        var seat = CreateSeat();

        // Act
        seat.Lock(_userId, TimeSpan.FromMinutes(2));

        // Assert
        var domainEvent = seat.DomainEvents.ShouldHaveSingleItem();
        var lockedEvent = domainEvent.ShouldBeOfType<SeatLockedEvent>();
        lockedEvent.SessionId.ShouldBe(_sessionId);
        lockedEvent.AreaId.ShouldBe(_areaId);
        lockedEvent.SeatNumber.ShouldBe(SeatNumber);
        lockedEvent.LockedBy.ShouldBe(_userId);
    }

    [Fact]
    public void Lock_WhenAlreadyLocked_ShouldReturnError()
    {
        // Arrange
        var seat = CreateSeat();
        seat.Lock(_userId, TimeSpan.FromMinutes(2));
        var anotherUser = UserId.New();

        // Act
        var result = seat.Lock(anotherUser, TimeSpan.FromMinutes(2));

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Seat.NotAvailable");
    }

    [Fact]
    public void Lock_WhenSold_ShouldReturnError()
    {
        // Arrange
        var seat = CreateSeat();
        seat.Lock(_userId, TimeSpan.FromMinutes(2));
        seat.Sell(_userId, OrderId.New());

        // Act
        var result = seat.Lock(_userId, TimeSpan.FromMinutes(2));

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Seat.NotAvailable");
    }

    [Fact]
    public void Release_WhenLocked_ShouldSucceed()
    {
        // Arrange
        var seat = CreateSeat();
        seat.Lock(_userId, TimeSpan.FromMinutes(2));
        seat.ClearDomainEvents();

        // Act
        seat.Release();

        // Assert
        seat.Status.ShouldBe(SeatStatus.Available);
        seat.LockedBy.ShouldBeNull();
        seat.LockExpiresAt.ShouldBeNull();
    }

    [Fact]
    public void Release_WhenLocked_ShouldRaiseSeatReleasedEvent()
    {
        // Arrange
        var seat = CreateSeat();
        seat.Lock(_userId, TimeSpan.FromMinutes(2));
        seat.ClearDomainEvents();

        // Act
        seat.Release();

        // Assert
        var domainEvent = seat.DomainEvents.ShouldHaveSingleItem();
        var releasedEvent = domainEvent.ShouldBeOfType<SeatReleasedEvent>();
        releasedEvent.SessionId.ShouldBe(_sessionId);
    }

    [Fact]
    public void Sell_WhenLockedBySameUser_ShouldSucceed()
    {
        // Arrange
        var seat = CreateSeat();
        var orderId = OrderId.New();
        seat.Lock(_userId, TimeSpan.FromMinutes(2));
        seat.ClearDomainEvents();

        // Act
        var result = seat.Sell(_userId, orderId);

        // Assert
        result.IsError.ShouldBeFalse();
        seat.Status.ShouldBe(SeatStatus.Sold);
        seat.SoldToOrder.ShouldBe(orderId);
    }

    [Fact]
    public void Sell_WhenLockedBySameUser_ShouldRaiseSeatSoldEvent()
    {
        // Arrange
        var seat = CreateSeat();
        var orderId = OrderId.New();
        seat.Lock(_userId, TimeSpan.FromMinutes(2));
        seat.ClearDomainEvents();

        // Act
        seat.Sell(_userId, orderId);

        // Assert
        var domainEvent = seat.DomainEvents.ShouldHaveSingleItem();
        var soldEvent = domainEvent.ShouldBeOfType<SeatSoldEvent>();
        soldEvent.BuyerId.ShouldBe(_userId);
        soldEvent.OrderId.ShouldBe(orderId);
    }

    [Fact]
    public void Sell_WhenLockedByDifferentUser_ShouldReturnError()
    {
        // Arrange
        var seat = CreateSeat();
        seat.Lock(_userId, TimeSpan.FromMinutes(2));
        var anotherUser = UserId.New();

        // Act
        var result = seat.Sell(anotherUser, OrderId.New());

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Seat.NotLockedByUser");
    }

    [Fact]
    public void Sell_WhenNotLocked_ShouldReturnError()
    {
        // Arrange
        var seat = CreateSeat();

        // Act
        var result = seat.Sell(_userId, OrderId.New());

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Seat.NotLocked");
    }

    [Fact]
    public void IsLockExpired_WhenExpired_ShouldReturnTrue()
    {
        // Arrange
        var seat = CreateSeat();
        seat.Lock(_userId, TimeSpan.FromMilliseconds(1));
        Thread.Sleep(10);

        // Act & Assert
        seat.IsLockExpired().ShouldBeTrue();
    }

    [Fact]
    public void IsLockExpired_WhenNotExpired_ShouldReturnFalse()
    {
        // Arrange
        var seat = CreateSeat();
        seat.Lock(_userId, TimeSpan.FromMinutes(2));

        // Act & Assert
        seat.IsLockExpired().ShouldBeFalse();
    }

    #region Expired Lock Auto-Release Tests

    [Fact]
    public void Lock_WhenPreviousLockExpired_ShouldAutoReleaseAndSucceed()
    {
        // Arrange
        var seat = CreateSeat();
        var firstUser = UserId.New();
        var secondUser = UserId.New();
        seat.Lock(firstUser, TimeSpan.FromMilliseconds(1));
        Thread.Sleep(10);
        seat.ClearDomainEvents();

        // Act
        var result = seat.Lock(secondUser, TimeSpan.FromMinutes(2));

        // Assert
        result.IsError.ShouldBeFalse();
        seat.Status.ShouldBe(SeatStatus.Locked);
        seat.LockedBy.ShouldBe(secondUser);
    }

    [Fact]
    public void Lock_WhenPreviousLockExpired_ShouldRaiseBothReleasedAndLockedEvents()
    {
        // Arrange
        var seat = CreateSeat();
        var firstUser = UserId.New();
        var secondUser = UserId.New();
        seat.Lock(firstUser, TimeSpan.FromMilliseconds(1));
        Thread.Sleep(10);
        seat.ClearDomainEvents();

        // Act
        seat.Lock(secondUser, TimeSpan.FromMinutes(2));

        // Assert
        seat.DomainEvents.Count.ShouldBe(2);
        seat.DomainEvents[0].ShouldBeOfType<SeatReleasedEvent>();
        seat.DomainEvents[1].ShouldBeOfType<SeatLockedEvent>();
    }

    [Fact]
    public void Lock_WhenPreviousLockNotExpired_ShouldReturnError()
    {
        // Arrange
        var seat = CreateSeat();
        var firstUser = UserId.New();
        var secondUser = UserId.New();
        seat.Lock(firstUser, TimeSpan.FromMinutes(2));

        // Act
        var result = seat.Lock(secondUser, TimeSpan.FromMinutes(2));

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Seat.NotAvailable");
        seat.LockedBy.ShouldBe(firstUser);
    }

    [Fact]
    public void Lock_WhenSameUserRelockAfterExpired_ShouldSucceed()
    {
        // Arrange
        var seat = CreateSeat();
        seat.Lock(_userId, TimeSpan.FromMilliseconds(1));
        Thread.Sleep(10);

        // Act
        var result = seat.Lock(_userId, TimeSpan.FromMinutes(2));

        // Assert
        result.IsError.ShouldBeFalse();
        seat.LockedBy.ShouldBe(_userId);
    }

    #endregion
}