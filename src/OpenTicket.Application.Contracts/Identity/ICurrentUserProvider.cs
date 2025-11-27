namespace OpenTicket.Application.Contracts.Identity;

/// <summary>
/// Provides access to the current authenticated user.
/// Implementations can be based on OAuth, JWT, or mock for testing.
/// </summary>
public interface ICurrentUserProvider
{
    /// <summary>
    /// Gets the current authenticated user.
    /// Returns Anonymous user if not authenticated.
    /// </summary>
    CurrentUser CurrentUser { get; }

    /// <summary>
    /// Checks if the current user is authenticated.
    /// </summary>
    bool IsAuthenticated => CurrentUser.IsAuthenticated;

    /// <summary>
    /// Gets the current user's email.
    /// Throws if user is not authenticated.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Thrown when user is not authenticated.</exception>
    string GetRequiredEmail()
    {
        if (!IsAuthenticated)
            throw new UnauthorizedAccessException("User is not authenticated");
        return CurrentUser.Email;
    }
}
