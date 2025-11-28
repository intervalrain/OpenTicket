using ErrorOr;
using OpenTicket.Domain.Shared.Identities;

namespace OpenTicket.Application.Contracts.RateLimiting;

/// <summary>
/// Service for handling rate limiting checks.
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Checks if the user has exceeded their rate limit for a specific action.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="action">The action being rate limited.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success if within limits, or an error.</returns>
    Task<ErrorOr<Success>> CheckRateLimitAsync(
        UserId userId,
        RateLimitedAction action,
        CancellationToken ct = default);

    /// <summary>
    /// Records that a rate-limited action was performed.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="action">The action performed.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RecordActionAsync(
        UserId userId,
        RateLimitedAction action,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the remaining quota for a user and action.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="action">The action.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The remaining count, or -1 for unlimited.</returns>
    Task<int> GetRemainingQuotaAsync(
        UserId userId,
        RateLimitedAction action,
        CancellationToken ct = default);
}

/// <summary>
/// Actions that are subject to rate limiting.
/// </summary>
public enum RateLimitedAction
{
    /// <summary>
    /// Creating a note.
    /// Non-subscribers: 3/day, Subscribers: unlimited
    /// </summary>
    CreateNote
}
