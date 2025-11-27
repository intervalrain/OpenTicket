namespace OpenTicket.Infrastructure.Resilience.Abstractions;

/// <summary>
/// Configuration options for resilience policies.
/// </summary>
public class ResilienceOptions
{
    public const string SectionName = "Resilience";

    /// <summary>
    /// Maximum number of retry attempts. Default is 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay between retry attempts. Default is 1 second.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between retry attempts. Default is 30 seconds.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to use exponential backoff for retries. Default is true.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Jitter factor for randomizing retry delays (0-1). Default is 0.2 (20%).
    /// Set to 0 to disable jitter.
    /// </summary>
    public double JitterFactor { get; set; } = 0.2;

    /// <summary>
    /// Whether to enable circuit breaker. Default is true.
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <summary>
    /// Duration the circuit stays open before allowing a test request. Default is 30 seconds.
    /// </summary>
    public TimeSpan CircuitBreakerDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Sampling duration for failure rate calculation. Default is 60 seconds.
    /// </summary>
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Minimum throughput before circuit breaker can trip. Default is 10 requests.
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Failure ratio threshold to trip the circuit breaker (0-1). Default is 0.5 (50%).
    /// </summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>
    /// Timeout for individual operations. Null means no timeout.
    /// </summary>
    public TimeSpan? Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
