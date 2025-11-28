using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTicket.Application.Contracts.Identity;

namespace OpenTicket.Infrastructure.Identity.OAuth;

/// <summary>
/// JWT token generation service.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly OAuthOptions _options;

    public JwtTokenService(IOptions<OAuthOptions> options)
    {
        _options = options.Value;
    }

    public string GenerateAccessToken(CurrentUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.Value.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.Name),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("has_subscription", user.HasSubscription.ToString().ToLower())
        };

        // Add roles as claims
        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (user.Provider != null)
        {
            claims.Add(new Claim("provider", user.Provider));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Jwt.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Jwt.Issuer,
            audience: _options.Jwt.Audience,
            claims: claims,
            expires: GetAccessTokenExpiration(),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public DateTime GetAccessTokenExpiration()
    {
        return DateTime.UtcNow.AddMinutes(_options.Jwt.AccessTokenExpirationMinutes);
    }
}
