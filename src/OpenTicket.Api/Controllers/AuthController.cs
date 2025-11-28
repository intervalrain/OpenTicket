using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenTicket.Application.Contracts.Identity;
using OpenTicket.Infrastructure.Identity.OAuth;

namespace OpenTicket.Api.Controllers;

/// <summary>
/// Authentication endpoints for OAuth login and token management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ITokenService? _tokenService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly EnabledOAuthProviders? _enabledProviders;

    public AuthController(
        ICurrentUserProvider currentUserProvider,
        ITokenService? tokenService = null,
        EnabledOAuthProviders? enabledProviders = null)
    {
        _currentUserProvider = currentUserProvider;
        _tokenService = tokenService;
        _enabledProviders = enabledProviders;
    }

    /// <summary>
    /// Get available OAuth providers.
    /// </summary>
    [HttpGet("providers")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProvidersResponse), StatusCodes.Status200OK)]
    public IActionResult GetProviders()
    {
        var providers = _enabledProviders?.GetProviderNames() ?? [];
        return Ok(new ProvidersResponse(providers));
    }

    /// <summary>
    /// Initiate OAuth login flow.
    /// Redirects to the OAuth provider's authorization page.
    /// </summary>
    /// <param name="provider">OAuth provider name (Google, Facebook, GitHub, Microsoft, Apple)</param>
    /// <param name="returnUrl">URL to redirect after successful authentication</param>
    [HttpGet("login/{provider}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Login(string provider, [FromQuery] string? returnUrl = null)
    {
        if (_enabledProviders is null)
        {
            return BadRequest(new { error = "OAuth is not configured. Using mock authentication." });
        }

        var normalizedProvider = NormalizeProviderName(provider);
        if (string.IsNullOrEmpty(normalizedProvider))
        {
            return BadRequest(new { error = $"Unknown provider: {provider}" });
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(Callback), new { returnUrl }),
            Items = { { "provider", normalizedProvider } }
        };

        return Challenge(properties, normalizedProvider);
    }

    /// <summary>
    /// OAuth callback endpoint.
    /// Processes the OAuth response and issues a JWT token.
    /// </summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Callback([FromQuery] string? returnUrl = null)
    {
        var authenticateResult = await HttpContext.AuthenticateAsync("External");

        if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
        {
            return Unauthorized(new { error = "OAuth authentication failed" });
        }

        if (_tokenService is null)
        {
            return BadRequest(new { error = "Token service not configured" });
        }

        // Extract user info from OAuth claims
        var claims = authenticateResult.Principal.Claims.ToList();
        var userId = claims.FirstOrDefault(c => c.Type == "sub")?.Value
                  ?? claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? Guid.NewGuid().ToString();

        var email = claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
        var name = claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Name)?.Value ?? "";
        var provider = authenticateResult.Properties?.Items["provider"] ?? "OAuth";

        // Create CurrentUser and generate JWT
        var currentUser = new CurrentUser
        {
            Id = OpenTicket.Domain.Shared.Identities.UserId.From(Guid.TryParse(userId, out var uid) ? uid : Guid.NewGuid()),
            Email = email,
            Name = name,
            Provider = provider,
            Roles = [Roles.User],
            IsAuthenticated = true,
            HasSubscription = false
        };

        var token = _tokenService.GenerateAccessToken(currentUser);
        var expiresAt = _tokenService.GetAccessTokenExpiration();

        // Clear external cookie
        await HttpContext.SignOutAsync("External");

        // Return token or redirect
        if (!string.IsNullOrEmpty(returnUrl))
        {
            var separator = returnUrl.Contains('?') ? "&" : "?";
            return Redirect($"{returnUrl}{separator}token={token}");
        }

        return Ok(new AuthTokenResponse(token, "Bearer", (int)(expiresAt - DateTime.UtcNow).TotalSeconds));
    }

    /// <summary>
    /// Get current user information.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        var user = _currentUserProvider.CurrentUser;

        if (!user.IsAuthenticated)
        {
            return Unauthorized();
        }

        return Ok(new CurrentUserResponse(
            user.Id.Value,
            user.Email,
            user.Name,
            user.Provider,
            user.Roles.ToList(),
            user.HasSubscription,
            user.IsAdmin));
    }

    /// <summary>
    /// Refresh JWT token.
    /// </summary>
    [HttpPost("refresh")]
    [Authorize]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult RefreshToken()
    {
        if (_tokenService is null)
        {
            return BadRequest(new { error = "Token service not configured" });
        }

        var user = _currentUserProvider.CurrentUser;

        if (!user.IsAuthenticated)
        {
            return Unauthorized();
        }

        var token = _tokenService.GenerateAccessToken(user);
        var expiresAt = _tokenService.GetAccessTokenExpiration();
        return Ok(new AuthTokenResponse(token, "Bearer", (int)(expiresAt - DateTime.UtcNow).TotalSeconds));
    }

    private static string? NormalizeProviderName(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "google" => "Google",
            "facebook" => "Facebook",
            "github" => "GitHub",
            "microsoft" => "Microsoft",
            "apple" => "Apple",
            _ => null
        };
    }
}

// Response DTOs
public record ProvidersResponse(IReadOnlyList<string> Providers);
public record AuthTokenResponse(string AccessToken, string TokenType, int ExpiresIn);
public record CurrentUserResponse(
    Guid Id,
    string Email,
    string Name,
    string? Provider,
    List<string> Roles,
    bool HasSubscription,
    bool IsAdmin);
