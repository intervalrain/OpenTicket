namespace OpenTicket.Infrastructure.Resilience.Abstractions;

/// <summary>
/// Provides resilience capabilities such as retry, circuit breaker, and timeout.
/// </summary>
public interface IResilienceService
{
    /// <summary>
    /// Executes an action with resilience policies (retry, circuit breaker, timeout).
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="action">The action to execute.</param>
    /// <param name="context">Optional context for logging and tracking.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the action.</returns>
    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        ResilienceContext? context = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes an action with resilience policies (retry, circuit breaker, timeout).
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="context">Optional context for logging and tracking.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        ResilienceContext? context = null,
        CancellationToken ct = default);
}
