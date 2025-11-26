namespace OpenTicket.Ddd.Application.Cqrs.Audit;

/// <summary>
/// Provides access to audit context information like trace ID, correlation ID, and user info.
/// </summary>
public interface IAuditContext
{
    /// <summary>
    /// Gets or sets the trace ID for the current request.
    /// </summary>
    string TraceId { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID for tracking related operations across services.
    /// </summary>
    string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the user ID for the current request.
    /// </summary>
    string? UserId { get; set; }

    /// <summary>
    /// Gets the timestamp when the request started.
    /// </summary>
    DateTime RequestStartedAt { get; }
}
