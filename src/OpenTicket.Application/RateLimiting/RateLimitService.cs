using ErrorOr;
using OpenTicket.Application.Contracts.Identity;
using OpenTicket.Application.Contracts.RateLimiting;
using OpenTicket.Domain.Shared.Identities;
using OpenTicket.Infrastructure.Cache.Abstractions;

namespace OpenTicket.Application.RateLimiting;

/// <summary>
/// Rate limiting service implementation using distributed cache.
/// </summary>
public sealed class RateLimitService : IRateLimitService
{
    private readonly IDistributedCache _cache;
    private readonly ICurrentUserProvider _currentUserProvider;

    private const int NonSubscriberDailyNoteLimit = 3;

    public RateLimitService(
        IDistributedCache cache,
        ICurrentUserProvider currentUserProvider)
    {
        _cache = cache;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<ErrorOr<Success>> CheckRateLimitAsync(
        UserId userId,
        RateLimitedAction action,
        CancellationToken ct = default)
    {
        var currentUser = _currentUserProvider.CurrentUser;

        // Admins and subscribers have no rate limits
        if (currentUser.IsAdmin || currentUser.HasSubscription)
        {
            return Result.Success;
        }

        var limit = GetLimit(action);
        if (limit < 0) // -1 means unlimited
        {
            return Result.Success;
        }

        var key = GetRateLimitKey(userId, action);
        var count = await _cache.GetAsync<int?>(key, ct) ?? 0;

        if (count >= limit)
        {
            return Error.Forbidden(
                "RateLimit.Exceeded",
                $"Rate limit exceeded for {action}. Daily limit: {limit}. Upgrade to a subscription for unlimited access.");
        }

        return Result.Success;
    }

    public async Task RecordActionAsync(
        UserId userId,
        RateLimitedAction action,
        CancellationToken ct = default)
    {
        var currentUser = _currentUserProvider.CurrentUser;

        // Don't record for admins and subscribers
        if (currentUser.IsAdmin || currentUser.HasSubscription)
        {
            return;
        }

        var key = GetRateLimitKey(userId, action);
        var count = await _cache.GetAsync<int?>(key, ct) ?? 0;

        // Set with TTL until end of day (UTC)
        var ttl = GetTimeUntilMidnightUtc();
        await _cache.SetAsync(key, count + 1, ttl, ct);
    }

    public async Task<int> GetRemainingQuotaAsync(
        UserId userId,
        RateLimitedAction action,
        CancellationToken ct = default)
    {
        var currentUser = _currentUserProvider.CurrentUser;

        // Admins and subscribers have unlimited quota
        if (currentUser.IsAdmin || currentUser.HasSubscription)
        {
            return -1; // Unlimited
        }

        var limit = GetLimit(action);
        if (limit < 0)
        {
            return -1;
        }

        var key = GetRateLimitKey(userId, action);
        var count = await _cache.GetAsync<int?>(key, ct) ?? 0;

        return Math.Max(0, limit - count);
    }

    private static int GetLimit(RateLimitedAction action) => action switch
    {
        RateLimitedAction.CreateNote => NonSubscriberDailyNoteLimit,
        _ => -1
    };

    private static string GetRateLimitKey(UserId userId, RateLimitedAction action)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        return $"ratelimit:{action}:{userId.Value}:{today}";
    }

    private static TimeSpan GetTimeUntilMidnightUtc()
    {
        var now = DateTime.UtcNow;
        var midnight = now.Date.AddDays(1);
        return midnight - now;
    }
}
