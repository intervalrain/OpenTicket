using OpenTicket.Ddd.Domain;
using OpenTicket.Domain.Shared.Identities;

namespace OpenTicket.Domain.Shared.Tickets.Events;

public sealed record SeatSoldEvent(
    SessionId SessionId,
    AreaId AreaId,
    string SeatNumber,
    UserId BuyerId,
    OrderId OrderId) : DomainEvent;