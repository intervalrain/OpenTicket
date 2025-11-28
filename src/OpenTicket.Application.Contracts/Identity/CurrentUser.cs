using OpenTicket.Domain.Shared.Identities;

namespace OpenTicket.Application.Contracts.Identity;

/// <summary>
/// Represents the current authenticated user.
/// </summary>
public record CurrentUser
{
    /// <summary>
    /// The user's unique identifier.
    /// </summary>
    public UserId Id { get; init; }

    /// <summary>
    /// The user's email address.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// The user's display name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Whether the user is authenticated.
    /// </summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    /// The authentication provider (e.g., "Google", "GitHub", "Mock").
    /// </summary>
    public string? Provider { get; init; }

    /// <summary>
    /// User roles for authorization.
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// Whether the user has an active subscription.
    /// Subscribers have unlimited access to certain features.
    /// </summary>
    public bool HasSubscription { get; init; }

    /// <summary>
    /// Checks if the user has a specific role.
    /// </summary>
    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if the user is an admin.
    /// </summary>
    public bool IsAdmin => IsInRole(Identity.Roles.Admin);

    /// <summary>
    /// Creates an anonymous (unauthenticated) user.
    /// </summary>
    public static CurrentUser Anonymous => new()
    {
        Id = default,
        Email = string.Empty,
        Name = "Anonymous",
        IsAuthenticated = false
    };
}
