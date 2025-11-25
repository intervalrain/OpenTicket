using OpenTicket.Ddd.Domain;
using OpenTicket.Domain.Shared.Identities;

namespace OpenTicket.Domain.Shared.Tickets.Events;

public sealed record SeatReleasedEvent(
    SessionId SessionId,
    AreaId AreaId,
    string SeatNumber) : DomainEvent;