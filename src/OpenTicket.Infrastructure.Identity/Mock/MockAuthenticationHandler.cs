using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OpenTicket.Infrastructure.Identity.Mock;

/// <summary>
/// Mock authentication handler that always succeeds with claims from MockUserOptions.
/// Used for development and testing when no real authentication is configured.
/// </summary>
public class MockAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly MockUserOptions _mockUserOptions;

    public MockAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<MockUserOptions> mockUserOptions)
        : base(options, logger, encoder)
    {
        _mockUserOptions = mockUserOptions.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _mockUserOptions.UserId.ToString()),
            new(ClaimTypes.Email, _mockUserOptions.Email),
            new(ClaimTypes.Name, _mockUserOptions.Name),
            new("provider", "Mock")
        };

        // Add role claims
        foreach (var role in _mockUserOptions.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Add subscription claim
        claims.Add(new Claim("has_subscription", _mockUserOptions.HasSubscription.ToString().ToLowerInvariant()));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
