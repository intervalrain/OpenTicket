using System.Text;
using Microsoft.AspNetCore.Authentication;
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
    /// Configures a pass-through authentication handler that always succeeds.
    /// </summary>
    public static IServiceCollection AddMockIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MockUserOptions>(
            configuration.GetSection(MockUserOptions.SectionName));

        // Add authentication with mock handler that always succeeds
        services.AddAuthentication("Mock")
            .AddScheme<AuthenticationSchemeOptions, MockAuthenticationHandler>("Mock", null);

        // Add authorization
        services.AddAuthorization();

        services.AddHttpContextAccessor();
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
    /// Adds OAuth/JWT identity provider for production with all providers from configuration.
    /// </summary>
    public static IServiceCollection AddOAuthIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var oauthOptions = configuration.GetSection(OAuthOptions.SectionName).Get<OAuthOptions>()
            ?? new OAuthOptions();

        // Determine which providers to enable based on configuration
        var providers = OAuthProviders.None;

        if (oauthOptions.Google?.Enabled == true)
            providers |= OAuthProviders.Google;
        if (oauthOptions.Facebook?.Enabled == true)
            providers |= OAuthProviders.Facebook;
        if (oauthOptions.GitHub?.Enabled == true)
            providers |= OAuthProviders.GitHub;
        if (oauthOptions.Microsoft?.Enabled == true)
            providers |= OAuthProviders.Microsoft;
        if (oauthOptions.Apple?.Enabled == true)
            providers |= OAuthProviders.Apple;

        return AddOAuthIdentity(services, configuration, providers);
    }

    /// <summary>
    /// Adds OAuth/JWT identity provider with specified providers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="providers">The OAuth providers to enable.</param>
    /// <example>
    /// <code>
    /// services.AddOAuthIdentity(
    ///     configuration,
    ///     OAuthProviders.Google | OAuthProviders.Facebook | OAuthProviders.GitHub);
    /// </code>
    /// </example>
    public static IServiceCollection AddOAuthIdentity(
        this IServiceCollection services,
        IConfiguration configuration,
        OAuthProviders providers)
    {
        var oauthOptions = configuration.GetSection(OAuthOptions.SectionName).Get<OAuthOptions>()
            ?? new OAuthOptions();

        services.Configure<OAuthOptions>(configuration.GetSection(OAuthOptions.SectionName));

        // Build authentication with JWT as default
        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = "External";
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
        })
        .AddCookie("External", options =>
        {
            options.Cookie.Name = "OpenTicket.External";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        });

        // Add OAuth providers based on flags
        if (providers.HasFlag(OAuthProviders.Google) && oauthOptions.Google != null)
        {
            authBuilder.AddGoogle(options =>
            {
                options.ClientId = oauthOptions.Google.ClientId;
                options.ClientSecret = oauthOptions.Google.ClientSecret;
                options.CallbackPath = "/signin-google";
                options.SaveTokens = true;
            });
        }

        if (providers.HasFlag(OAuthProviders.Facebook) && oauthOptions.Facebook != null)
        {
            authBuilder.AddFacebook(options =>
            {
                options.AppId = oauthOptions.Facebook.ClientId;
                options.AppSecret = oauthOptions.Facebook.ClientSecret;
                options.CallbackPath = "/signin-facebook";
                options.SaveTokens = true;
            });
        }

        if (providers.HasFlag(OAuthProviders.GitHub) && oauthOptions.GitHub != null)
        {
            authBuilder.AddOAuth("GitHub", options =>
            {
                options.ClientId = oauthOptions.GitHub.ClientId;
                options.ClientSecret = oauthOptions.GitHub.ClientSecret;
                options.CallbackPath = "/signin-github";
                options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
                options.TokenEndpoint = "https://github.com/login/oauth/access_token";
                options.UserInformationEndpoint = "https://api.github.com/user";
                options.SaveTokens = true;
                options.ClaimActions.MapJsonKey("sub", "id");
                options.ClaimActions.MapJsonKey("name", "name");
                options.ClaimActions.MapJsonKey("email", "email");
                options.ClaimActions.MapJsonKey("avatar_url", "avatar_url");
            });
        }

        if (providers.HasFlag(OAuthProviders.Microsoft) && oauthOptions.Microsoft != null)
        {
            authBuilder.AddMicrosoftAccount(options =>
            {
                options.ClientId = oauthOptions.Microsoft.ClientId;
                options.ClientSecret = oauthOptions.Microsoft.ClientSecret;
                options.CallbackPath = "/signin-microsoft";
                options.SaveTokens = true;
            });
        }

        if (providers.HasFlag(OAuthProviders.Apple) && oauthOptions.Apple != null)
        {
            authBuilder.AddOAuth("Apple", options =>
            {
                options.ClientId = oauthOptions.Apple.ClientId;
                options.ClientSecret = oauthOptions.Apple.ClientSecret; // Generated dynamically for Apple
                options.CallbackPath = "/signin-apple";
                options.AuthorizationEndpoint = "https://appleid.apple.com/auth/authorize";
                options.TokenEndpoint = "https://appleid.apple.com/auth/token";
                options.SaveTokens = true;
                options.Scope.Add("name");
                options.Scope.Add("email");
            });
        }

        // Register services
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserProvider, JwtCurrentUserProvider>();
        services.AddScoped<ITokenService, JwtTokenService>();

        // Register enabled providers for runtime access
        services.AddSingleton(new EnabledOAuthProviders(providers));

        return services;
    }
}
