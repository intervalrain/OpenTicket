using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTicket.Ddd.Application.Cqrs.Audit;

namespace OpenTicket.Ddd.Application.Cqrs.Behaviors;

/// <summary>
/// Pipeline behavior that adds audit information (trace ID, correlation ID) to the request context
/// and logs audit trail for commands.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <typeparam name="TResult">The type of result.</typeparam>
public sealed class AuditBehavior<TRequest, TResult> : IPipelineBehavior<TRequest, TResult>
    where TRequest : notnull
{
    private readonly IAuditContext _auditContext;
    private readonly ILogger<AuditBehavior<TRequest, TResult>> _logger;

    public AuditBehavior(IAuditContext auditContext, ILogger<AuditBehavior<TRequest, TResult>> logger)
    {
        _auditContext = auditContext;
        _logger = logger;
    }

    public async Task<TResult> HandleAsync(TRequest request, Func<Task<TResult>> next, CancellationToken ct = default)
    {
        var requestName = typeof(TRequest).Name;
        var traceId = _auditContext.TraceId;
        var correlationId = _auditContext.CorrelationId;
        var userId = _auditContext.UserId;

        // Set trace ID in Activity for distributed tracing integration
        Activity.Current?.SetTag("trace.id", traceId);
        Activity.Current?.SetTag("correlation.id", correlationId);
        Activity.Current?.SetTag("user.id", userId);

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = traceId,
            ["CorrelationId"] = correlationId,
            ["UserId"] = userId,
            ["RequestType"] = requestName
        });

        _logger.LogDebug(
            "Processing {RequestName} | TraceId: {TraceId} | CorrelationId: {CorrelationId} | UserId: {UserId}",
            requestName, traceId, correlationId, userId);

        try
        {
            var result = await next();

            // Only log audit trail for commands (state-changing operations)
            if (IsCommand())
            {
                _logger.LogInformation(
                    "Audit: {RequestName} completed | TraceId: {TraceId} | UserId: {UserId}",
                    requestName, traceId, userId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Audit: {RequestName} failed | TraceId: {TraceId} | UserId: {UserId} | Error: {ErrorMessage}",
                requestName, traceId, userId, ex.Message);

            throw;
        }
    }

    private static bool IsCommand()
    {
        var requestType = typeof(TRequest);
        return requestType.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));
    }
}
