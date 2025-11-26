namespace OpenTicket.Ddd.Application.Cqrs;

/// <summary>
/// Pipeline behavior for processing requests.
/// Behaviors are executed in order and can perform cross-cutting concerns
/// like logging, validation, transaction management, etc.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResult">The type of result returned.</typeparam>
public interface IPipelineBehavior<in TRequest, TResult>
{
    /// <summary>
    /// Handles the request with the ability to invoke the next behavior in the pipeline.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <param name="next">The delegate to invoke the next behavior or handler.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the request.</returns>
    Task<TResult> HandleAsync(TRequest request, Func<Task<TResult>> next, CancellationToken ct = default);
}
