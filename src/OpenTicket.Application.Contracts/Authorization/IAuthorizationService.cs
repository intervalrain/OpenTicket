using ErrorOr;
using OpenTicket.Application.Contracts.Identity;

namespace OpenTicket.Application.Contracts.Authorization;

/// <summary>
/// Service for handling authorization checks.
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Checks if the current user can perform an action on a resource.
    /// </summary>
    /// <param name="resource">The resource being accessed.</param>
    /// <param name="action">The action being performed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success if authorized, or an error.</returns>
    Task<ErrorOr<Success>> AuthorizeAsync<TResource>(
        TResource resource,
        ResourceAction action,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the current user.
    /// </summary>
    CurrentUser CurrentUser { get; }

    /// <summary>
    /// Checks if the current user is an admin.
    /// </summary>
    bool IsAdmin { get; }
}

/// <summary>
/// Actions that can be performed on resources.
/// </summary>
public enum ResourceAction
{
    Read,
    Create,
    Update,
    Delete
}
