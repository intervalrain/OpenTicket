using OpenTicket.Ddd.Infrastructure;

namespace OpenTicket.Ddd.Application.Cqrs.Behaviors;

/// <summary>
/// Pipeline behavior that wraps command execution in a transaction.
/// Only applies to commands (not queries) to maintain CQRS separation.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <typeparam name="TResult">The type of result.</typeparam>
public sealed class TransactionBehavior<TRequest, TResult> : IPipelineBehavior<TRequest, TResult>
    where TRequest : notnull
{
    private readonly IUnitOfWork _unitOfWork;

    public TransactionBehavior(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<TResult> HandleAsync(TRequest request, Func<Task<TResult>> next, CancellationToken ct = default)
    {
        // Only apply transaction to commands, not queries
        if (!IsCommand())
            return await next();

        var result = await next();

        await _unitOfWork.SaveChangesAsync(ct);

        return result;
    }

    private static bool IsCommand()
    {
        var requestType = typeof(TRequest);
        return requestType.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));
    }
}
