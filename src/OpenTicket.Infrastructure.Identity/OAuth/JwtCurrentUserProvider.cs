using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using OpenTicket.Application.Contracts.Identity;
using OpenTicket.Domain.Shared.Identities;

namespace OpenTicket.Infrastructure.Identity.OAuth;

/// <summary>
/// JWT-based current user provider that extracts user info from JWT claims.
/// </summary>
public sealed class JwtCurrentUserProvider : ICurrentUserProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private CurrentUser? _cachedUser;

    public JwtCurrentUserProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public CurrentUser CurrentUser => _cachedUser ??= GetCurrentUser();

    private CurrentUser GetCurrentUser()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return CurrentUser.Anonymous;
        }

        var claims = httpContext.User.Claims.ToList();

        var userIdClaim = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
            ?? claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return CurrentUser.Anonymous;
        }

        var email = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value
            ?? claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value
            ?? string.Empty;

        var name = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name)?.Value
            ?? claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value
            ?? string.Empty;

        var roles = claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        var hasSubscription = claims
            .FirstOrDefault(c => c.Type == "has_subscription")?.Value
            .Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

        var provider = claims.FirstOrDefault(c => c.Type == "provider")?.Value;

        return new CurrentUser
        {
            Id = UserId.From(userId),
            Email = email,
            Name = name,
            IsAuthenticated = true,
            Provider = provider,
            Roles = roles,
            HasSubscription = hasSubscription
        };
    }
}
