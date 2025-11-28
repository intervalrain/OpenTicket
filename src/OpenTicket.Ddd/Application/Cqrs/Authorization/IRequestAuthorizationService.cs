using ErrorOr;

namespace OpenTicket.Ddd.Application.Cqrs.Authorization;

/// <summary>
/// Service for authorizing requests based on roles, permissions, and policies.
/// Used by <see cref="Behaviors.AuthorizationBehavior{TRequest,TResponse}"/> for AOP-style authorization.
/// </summary>
public interface IRequestAuthorizationService
{
    /// <summary>
    /// Authorizes the current user for a request based on required roles, permissions, and policies.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="request">The request being authorized.</param>
    /// <param name="requiredRoles">Roles the user must have (at least one).</param>
    /// <param name="requiredPermissions">Permissions the user must have (all).</param>
    /// <param name="requiredPolicies">Policies that must pass (all).</param>
    /// <returns>Success if authorized, or an error.</returns>
    ErrorOr<Success> AuthorizeCurrentUser<TRequest>(
        TRequest request,
        IReadOnlyList<string> requiredRoles,
        IReadOnlyList<string> requiredPermissions,
        IReadOnlyList<string> requiredPolicies);
}
