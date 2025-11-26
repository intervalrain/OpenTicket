using OpenTicket.Ddd.Application;
using OpenTicket.Domain.Shared.Identities;
using OpenTicket.Domain.Tickets.Entities;
using OpenTicket.Domain.Tickets.ValueObjects;

namespace OpenTicket.Domain.Tickets.Repositories;

public interface ISeatRepository : IRepository<Seat, SeatId>
{
    Task<IReadOnlyList<Seat>> GetBySessionAsync(SessionId sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<Seat>> GetBySessionAndAreaAsync(SessionId sessionId, AreaId areaId, CancellationToken ct = default);
}