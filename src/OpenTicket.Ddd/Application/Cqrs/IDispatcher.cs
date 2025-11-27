namespace OpenTicket.Ddd.Application.Cqrs;

/// <summary>
/// Dispatches commands and queries to their respective handlers.
/// Provides a MediatR-like API for CQRS pattern.
/// </summary>
public interface IDispatcher
{
    /// <summary>
    /// Sends a command to its handler.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the command.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the command.</returns>
    Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default);

    /// <summary>
    /// Sends a query to its handler.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the query.</typeparam>
    /// <param name="query">The query to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the query.</returns>
    Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default);
}
