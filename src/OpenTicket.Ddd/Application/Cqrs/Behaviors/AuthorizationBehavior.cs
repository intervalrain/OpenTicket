using System.Reflection;
using ErrorOr;
using OpenTicket.Ddd.Application.Cqrs.Authorization;

namespace OpenTicket.Ddd.Application.Cqrs.Behaviors;

/// <summary>
/// Pipeline behavior that enforces authorization based on <see cref="AuthorizeAttribute"/> decorations.
/// Only applies to requests that implement <see cref="IAuthorizeableRequest{TResponse}"/>.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <typeparam name="TResponse">The type of response (must implement IErrorOr).</typeparam>
public sealed class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAuthorizeableRequest<TResponse>
    where TResponse : IErrorOr
{
    private readonly IRequestAuthorizationService _authorizationService;

    public AuthorizationBehavior(IRequestAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public async Task<TResponse> HandleAsync(
        TRequest request,
        Func<Task<TResponse>> next,
        CancellationToken ct = default)
    {
        var authorizationAttributes = request.GetType()
            .GetCustomAttributes<AuthorizeAttribute>()
            .ToList();

        if (authorizationAttributes.Count == 0)
        {
            return await next();
        }

        var requiredPermissions = authorizationAttributes
            .SelectMany(attr => attr.Permissions?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [])
            .Select(p => p.Trim())
            .Distinct()
            .ToList();

        var requiredRoles = authorizationAttributes
            .SelectMany(attr => attr.Roles?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [])
            .Select(r => r.Trim())
            .Distinct()
            .ToList();

        var requiredPolicies = authorizationAttributes
            .SelectMany(attr => attr.Policies?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [])
            .Select(p => p.Trim())
            .Distinct()
            .ToList();

        var authorizationResult = _authorizationService.AuthorizeCurrentUser(
            request,
            requiredRoles,
            requiredPermissions,
            requiredPolicies);

        if (authorizationResult.IsError)
        {
            return (dynamic)authorizationResult.Errors;
        }

        return await next();
    }
}
