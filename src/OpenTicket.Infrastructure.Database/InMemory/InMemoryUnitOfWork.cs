using OpenTicket.Ddd.Infrastructure;

namespace OpenTicket.Infrastructure.Database.InMemory;

/// <summary>
/// In-memory implementation of IUnitOfWork for MVP mode.
/// Since in-memory repository commits immediately, this is a no-op.
/// </summary>
public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // In-memory operations are immediate, no pending changes to save
        return Task.FromResult(0);
    }
}
