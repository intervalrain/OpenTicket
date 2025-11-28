namespace OpenTicket.Infrastructure.Identity.OAuth;

/// <summary>
/// Flags enum for selecting OAuth providers to enable.
/// </summary>
[Flags]
public enum OAuthProviders
{
    /// <summary>
    /// No OAuth providers enabled.
    /// </summary>
    None = 0,

    /// <summary>
    /// Google OAuth provider.
    /// </summary>
    Google = 1 << 0,

    /// <summary>
    /// Facebook OAuth provider.
    /// </summary>
    Facebook = 1 << 1,

    /// <summary>
    /// GitHub OAuth provider.
    /// </summary>
    GitHub = 1 << 2,

    /// <summary>
    /// Microsoft OAuth provider.
    /// </summary>
    Microsoft = 1 << 3,

    /// <summary>
    /// Apple OAuth provider.
    /// </summary>
    Apple = 1 << 4,

    /// <summary>
    /// All OAuth providers.
    /// </summary>
    All = Google | Facebook | GitHub | Microsoft | Apple
}

/// <summary>
/// Configuration for enabled OAuth providers.
/// </summary>
public sealed class EnabledOAuthProviders(OAuthProviders providers)
{
    /// <summary>
    /// The enabled OAuth providers.
    /// </summary>
    public OAuthProviders Providers { get; } = providers;

    public bool IsEnabled(OAuthProviders provider) => Providers.HasFlag(provider);

    public IReadOnlyList<string> GetProviderNames()
    {
        var names = new List<string>();

        if (Providers.HasFlag(OAuthProviders.Google))
            names.Add("Google");
        if (Providers.HasFlag(OAuthProviders.Facebook))
            names.Add("Facebook");
        if (Providers.HasFlag(OAuthProviders.GitHub))
            names.Add("GitHub");
        if (Providers.HasFlag(OAuthProviders.Microsoft))
            names.Add("Microsoft");
        if (Providers.HasFlag(OAuthProviders.Apple))
            names.Add("Apple");

        return names;
    }
}
