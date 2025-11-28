namespace OpenTicket.Application.Contracts.Identity;

/// <summary>
/// Service for generating and validating JWT tokens.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates an access token for the user.
    /// </summary>
    string GenerateAccessToken(CurrentUser user);

    /// <summary>
    /// Generates a refresh token.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Gets the token expiration time.
    /// </summary>
    DateTime GetAccessTokenExpiration();
}

/// <summary>
/// Token response containing access and refresh tokens.
/// </summary>
public record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt);
