using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenTicket.Application.Contracts.Identity;
using OpenTicket.Infrastructure.Identity.Mock;
using OpenTicket.Infrastructure.Identity.OAuth;

namespace OpenTicket.Infrastructure.Identity;

/// <summary>
/// Identity provider options.
/// </summary>
public enum IdentityProvider
{
    Mock,
    OAuth
}

/// <summary>
/// Module for registering identity services.
/// </summary>
public static class OpenTicketInfrastructureIdentityModule
{
    /// <summary>
    /// Adds identity services based on the specified provider.
    /// </summary>
    public static IServiceCollection AddIdentity(
        this IServiceCollection services,
        IConfiguration configuration,
        IdentityProvider provider)
    {
        return provider switch
        {
            IdentityProvider.Mock => AddMockIdentity(services, configuration),
            IdentityProvider.OAuth => AddOAuthIdentity(services, configuration),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
    }

    /// <summary>
    /// Adds mock identity provider for development and testing.
    /// </summary>
    public static IServiceCollection AddMockIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MockUserOptions>(
            configuration.GetSection(MockUserOptions.SectionName));

        services.AddScoped<ICurrentUserProvider, MockCurrentUserProvider>();

        return services;
    }

    /// <summary>
    /// Adds mock identity provider with custom configuration.
    /// </summary>
    public static IServiceCollection AddMockIdentity(
        this IServiceCollection services,
        Action<MockUserOptions> configure)
    {
        services.Configure(configure);
        services.AddScoped<ICurrentUserProvider, MockCurrentUserProvider>();

        return services;
    }

    /// <summary>
    /// Adds OAuth/JWT identity provider for production.
    /// Supports Google, Facebook, GitHub, and Apple OAuth providers.
    /// </summary>
    public static IServiceCollection AddOAuthIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var oauthOptions = configuration.GetSection(OAuthOptions.SectionName).Get<OAuthOptions>()
            ?? new OAuthOptions();

        services.Configure<OAuthOptions>(configuration.GetSection(OAuthOptions.SectionName));

        // Add JWT Bearer authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = oauthOptions.Jwt.Issuer,
                ValidAudience = oauthOptions.Jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(oauthOptions.Jwt.SecretKey)),
                ClockSkew = TimeSpan.Zero
            };
        });

        // Register services
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserProvider, JwtCurrentUserProvider>();
        services.AddScoped<ITokenService, JwtTokenService>();

        return services;
    }
}
