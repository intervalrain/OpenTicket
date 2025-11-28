using ErrorOr;
using OpenTicket.Application.Contracts.Identity;
using OpenTicket.Ddd.Application.Cqrs.Authorization;

namespace OpenTicket.Application.Authorization;

/// <summary>
/// Implementation of request authorization service.
/// Handles role-based and permission-based authorization for CQRS requests.
/// </summary>
public sealed class RequestAuthorizationService : IRequestAuthorizationService
{
    private readonly ICurrentUserProvider _currentUserProvider;

    public RequestAuthorizationService(ICurrentUserProvider currentUserProvider)
    {
        _currentUserProvider = currentUserProvider;
    }

    public ErrorOr<Success> AuthorizeCurrentUser<TRequest>(
        TRequest request,
        IReadOnlyList<string> requiredRoles,
        IReadOnlyList<string> requiredPermissions,
        IReadOnlyList<string> requiredPolicies)
    {
        var currentUser = _currentUserProvider.CurrentUser;

        // Check if user is authenticated
        if (!currentUser.IsAuthenticated)
        {
            return Error.Unauthorized(
                "Authorization.Unauthenticated",
                "User is not authenticated.");
        }

        // Admin bypasses all authorization checks
        if (currentUser.IsAdmin)
        {
            return Result.Success;
        }

        // Check required roles (user must have at least one)
        if (requiredRoles.Count > 0)
        {
            var hasRequiredRole = requiredRoles.Any(role =>
                currentUser.Roles.Contains(role, StringComparer.OrdinalIgnoreCase));

            if (!hasRequiredRole)
            {
                return Error.Forbidden(
                    "Authorization.MissingRole",
                    $"User is missing required roles. Required one of: {string.Join(", ", requiredRoles)}");
            }
        }

        // Check required permissions (user must have all)
        // Note: Currently, permissions are not implemented in CurrentUser.
        // This is a placeholder for future permission-based authorization.
        if (requiredPermissions.Count > 0)
        {
            // TODO: Implement when permissions are added to CurrentUser
            // For now, we skip permission checks
        }

        // Check required policies (all must pass)
        // Note: Policy evaluation is not yet implemented.
        // This is a placeholder for future policy-based authorization.
        if (requiredPolicies.Count > 0)
        {
            // TODO: Implement when policy enforcement is added
            // For now, we skip policy checks
        }

        return Result.Success;
    }
}
