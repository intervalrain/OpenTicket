namespace OpenTicket.Infrastructure.Identity.OAuth;

/// <summary>
/// Configuration options for OAuth authentication.
/// </summary>
public class OAuthOptions
{
    public const string SectionName = "OAuth";

    /// <summary>
    /// JWT settings for token generation and validation.
    /// </summary>
    public JwtSettings Jwt { get; set; } = new();

    /// <summary>
    /// Google OAuth configuration.
    /// </summary>
    public OAuthProviderSettings? Google { get; set; }

    /// <summary>
    /// Facebook OAuth configuration.
    /// </summary>
    public OAuthProviderSettings? Facebook { get; set; }

    /// <summary>
    /// GitHub OAuth configuration.
    /// </summary>
    public OAuthProviderSettings? GitHub { get; set; }

    /// <summary>
    /// Apple OAuth configuration.
    /// </summary>
    public OAuthProviderSettings? Apple { get; set; }
}

/// <summary>
/// JWT token settings.
/// </summary>
public class JwtSettings
{
    /// <summary>
    /// Secret key for signing tokens.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Token issuer.
    /// </summary>
    public string Issuer { get; set; } = "OpenTicket";

    /// <summary>
    /// Token audience.
    /// </summary>
    public string Audience { get; set; } = "OpenTicket";

    /// <summary>
    /// Access token expiration in minutes.
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Refresh token expiration in days.
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;
}

/// <summary>
/// OAuth provider-specific settings.
/// </summary>
public class OAuthProviderSettings
{
    /// <summary>
    /// Whether this provider is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// OAuth client ID.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth client secret.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;
}