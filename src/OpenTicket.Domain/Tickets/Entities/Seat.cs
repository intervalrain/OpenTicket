using ErrorOr;
using OpenTicket.Ddd.Domain;
using OpenTicket.Domain.Shared.Identities;
using OpenTicket.Domain.Shared.Tickets.Enums;
using OpenTicket.Domain.Shared.Tickets.Events;
using OpenTicket.Domain.Tickets.ValueObjects;

namespace OpenTicket.Domain.Tickets.Entities;

public class Seat : AggregateRoot<SeatId>
{
    public SessionId SessionId { get; private set; }
    public AreaId AreaId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public SeatStatus Status { get; private set; }
    public UserId? LockedBy { get; private set; }
    public DateTime? LockExpiresAt { get; private set; }
    public OrderId? SoldToOrder { get; private set; }

    private Seat() { } // EF Core

    public static Seat Create(SessionId sessionId, AreaId areaId, string number)
    {
        var seat = new Seat
        {
            Id = SeatId.From(sessionId, areaId, number),
            SessionId = sessionId,
            AreaId = areaId,
            Number = number,
            Status = SeatStatus.Available
        };
        return seat;
    }

    public ErrorOr<Success> Lock(UserId userId, TimeSpan ttl)
    {
        if (IsLockExpired())
            Release();

        if (Status != SeatStatus.Available)
            return Error.Conflict("Seat.NotAvailable", "Seat is not available for locking");

        LockedBy = userId;
        LockExpiresAt = DateTime.UtcNow + ttl;
        Status = SeatStatus.Locked;

        AddDomainEvent(new SeatLockedEvent(SessionId, AreaId, Number, userId, LockExpiresAt.Value));

        return Result.Success;
    }

    public void Release()
    {
        if (Status != SeatStatus.Locked)
            return;

        var wasLocked = Status == SeatStatus.Locked;
        LockedBy = null;
        LockExpiresAt = null;
        Status = SeatStatus.Available;

        if (wasLocked)
            AddDomainEvent(new SeatReleasedEvent(SessionId, AreaId, Number));
    }

    public ErrorOr<Success> Sell(UserId buyerId, OrderId orderId)
    {
        if (Status != SeatStatus.Locked)
            return Error.Conflict("Seat.NotLocked", "Seat must be locked before selling");

        if (LockedBy != buyerId)
            return Error.Conflict("Seat.NotLockedByUser", "Seat is locked by another user");

        Status = SeatStatus.Sold;
        SoldToOrder = orderId;
        LockedBy = null;
        LockExpiresAt = null;

        AddDomainEvent(new SeatSoldEvent(SessionId, AreaId, Number, buyerId, orderId));

        return Result.Success;
    }

    public bool IsLockExpired()
    {
        if (Status != SeatStatus.Locked || LockExpiresAt is null)
            return false;

        return DateTime.UtcNow > LockExpiresAt.Value;
    }

    public override string ToString()
    {
        return Id.ToString();
    }
}