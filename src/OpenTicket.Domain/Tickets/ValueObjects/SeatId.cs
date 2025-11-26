using OpenTicket.Ddd.Domain;
using OpenTicket.Domain.Shared.Identities;

namespace OpenTicket.Domain.Tickets.ValueObjects;

public readonly record struct SeatId(
    SessionId SessionId,
    AreaId AreaId,
    string Number) : IStronglyTypedId<string>
{
    public string Value => $"{SessionId.Value}:{AreaId.Value}:{Number}";

    public override string ToString() => $"{SessionId.ShortId()}:{AreaId.ShortId()}:{Number}";

    public static SeatId From(SessionId sessionId, AreaId areaId, string number)
        => new(sessionId, areaId, number);
}