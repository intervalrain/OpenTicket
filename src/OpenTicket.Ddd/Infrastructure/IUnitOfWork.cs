namespace OpenTicket.Ddd.Infrastructure;

/// <summary>
/// Unit of Work pattern interface. Coordinates the persistence of multiple aggregates.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}