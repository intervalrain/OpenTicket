namespace OpenTicket.Infrastructure.Resilience.Abstractions;

/// <summary>
/// Context information for resilience operations, used for logging and tracking.
/// </summary>
public class ResilienceContext
{
    /// <summary>
    /// Name of the operation being executed.
    /// </summary>
    public string? OperationName { get; init; }

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Additional context specific to the operation.
    /// </summary>
    public IDictionary<string, object>? Properties { get; init; }
}
