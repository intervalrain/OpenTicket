namespace OpenTicket.Ddd.Application.Cqrs.Audit;

/// <summary>
/// Default implementation of IAuditContext.
/// Scoped per request to maintain trace information throughout the request lifecycle.
/// </summary>
public sealed class AuditContext : IAuditContext
{
    public string TraceId { get; set; } = Guid.NewGuid().ToString("N");
    public string? CorrelationId { get; set; }
    public string? UserId { get; set; }
    public DateTime RequestStartedAt { get; } = DateTime.UtcNow;
}
