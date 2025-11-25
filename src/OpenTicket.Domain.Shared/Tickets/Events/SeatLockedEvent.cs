using OpenTicket.Ddd.Domain;
using OpenTicket.Domain.Shared.Identities;

namespace OpenTicket.Domain.Shared.Tickets.Events;

public sealed record SeatLockedEvent(
    SessionId SessionId,
    AreaId AreaId,
    string SeatNumber,
    UserId LockedBy,
    DateTime ExpiresAt) : DomainEvent;