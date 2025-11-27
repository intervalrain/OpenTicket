using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTicket.Infrastructure.Resilience.Abstractions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using ResilienceContext = OpenTicket.Infrastructure.Resilience.Abstractions.ResilienceContext;

namespace OpenTicket.Infrastructure.Resilience.Polly;

/// <summary>
/// Polly-based implementation of IResilienceService.
/// Provides retry, circuit breaker, and timeout policies.
/// </summary>
public sealed class PollyResilienceService : IResilienceService
{
    private readonly ResilienceOptions _options;
    private readonly ILogger<PollyResilienceService> _logger;
    private readonly ResiliencePipeline _pipeline;

    public PollyResilienceService(
        IOptions<ResilienceOptions> options,
        ILogger<PollyResilienceService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _pipeline = BuildPipeline();
    }

    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        ResilienceContext? context = null,
        CancellationToken ct = default)
    {
        try
        {
            return await _pipeline.ExecuteAsync(
                async token => await action(token),
                ct);
        }
        catch (Exception ex)
        {
            LogFinalFailure(ex, context);
            throw;
        }
    }

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        ResilienceContext? context = null,
        CancellationToken ct = default)
    {
        try
        {
            await _pipeline.ExecuteAsync(
                async token =>
                {
                    await action(token);
                },
                ct);
        }
        catch (Exception ex)
        {
            LogFinalFailure(ex, context);
            throw;
        }
    }

    private ResiliencePipeline BuildPipeline()
    {
        var builder = new ResiliencePipelineBuilder();

        // Add timeout if configured
        if (_options.Timeout.HasValue)
        {
            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = _options.Timeout.Value,
                OnTimeout = args =>
                {
                    _logger.LogWarning(
                        "Operation timed out after {Timeout}",
                        args.Timeout);
                    return default;
                }
            });
        }

        // Add retry policy
        builder.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = _options.MaxRetryAttempts,
            BackoffType = _options.UseExponentialBackoff
                ? DelayBackoffType.Exponential
                : DelayBackoffType.Constant,
            Delay = _options.RetryDelay,
            MaxDelay = _options.MaxRetryDelay,
            UseJitter = _options.JitterFactor > 0,
            ShouldHandle = new PredicateBuilder()
                .Handle<Exception>(ex => IsTransientException(ex)),
            OnRetry = args =>
            {
                _logger.LogWarning(
                    args.Outcome.Exception,
                    "Retry attempt {AttemptNumber}/{MaxAttempts} after {Delay}ms. Exception: {ExceptionMessage}",
                    args.AttemptNumber,
                    _options.MaxRetryAttempts,
                    args.RetryDelay.TotalMilliseconds,
                    args.Outcome.Exception?.Message);
                return default;
            }
        });

        // Add circuit breaker if enabled
        if (_options.EnableCircuitBreaker)
        {
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = _options.CircuitBreakerFailureRatio,
                MinimumThroughput = _options.CircuitBreakerMinimumThroughput,
                SamplingDuration = _options.CircuitBreakerSamplingDuration,
                BreakDuration = _options.CircuitBreakerDuration,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => IsTransientException(ex)),
                OnOpened = args =>
                {
                    _logger.LogError(
                        "Circuit breaker opened for {Duration}. Too many failures detected.",
                        args.BreakDuration);
                    return default;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("Circuit breaker closed. Resuming normal operations.");
                    return default;
                },
                OnHalfOpened = _ =>
                {
                    _logger.LogInformation("Circuit breaker half-opened. Testing with next request.");
                    return default;
                }
            });
        }

        return builder.Build();
    }

    private static bool IsTransientException(Exception ex)
    {
        // Don't retry on cancellation
        if (ex is OperationCanceledException or TaskCanceledException)
            return false;

        // Don't retry if circuit is open
        if (ex is BrokenCircuitException)
            return false;

        // Always retry timeouts
        if (ex is TimeoutException)
            return true;

        // Check by exception type name for common transient patterns
        return IsTransientByType(ex);
    }

    private static bool IsTransientByType(Exception ex)
    {
        var typeName = ex.GetType().Name;
        return typeName.Contains("Transient", StringComparison.OrdinalIgnoreCase)
               || typeName.Contains("Timeout", StringComparison.OrdinalIgnoreCase)
               || typeName.Contains("Connection", StringComparison.OrdinalIgnoreCase)
               || typeName.Contains("Network", StringComparison.OrdinalIgnoreCase)
               || typeName.Contains("Unavailable", StringComparison.OrdinalIgnoreCase)
               || (ex.InnerException != null && IsTransientException(ex.InnerException));
    }

    private void LogFinalFailure(Exception ex, ResilienceContext? context)
    {
        _logger.LogError(
            ex,
            "Operation {OperationName} failed after all retries. CorrelationId: {CorrelationId}",
            context?.OperationName ?? "Unknown",
            context?.CorrelationId ?? "N/A");
    }
}
