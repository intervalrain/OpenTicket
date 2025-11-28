using ErrorOr;
using OpenTicket.Application.Contracts.Authorization;
using OpenTicket.Application.Contracts.Identity;
using OpenTicket.Domain.Notes.Entities;

namespace OpenTicket.Application.Authorization;

/// <summary>
/// Authorization service implementation.
/// </summary>
public sealed class AuthorizationService : IAuthorizationService
{
    private readonly ICurrentUserProvider _currentUserProvider;

    public AuthorizationService(ICurrentUserProvider currentUserProvider)
    {
        _currentUserProvider = currentUserProvider;
    }

    public CurrentUser CurrentUser => _currentUserProvider.CurrentUser;

    public bool IsAdmin => CurrentUser.IsAdmin;

    public Task<ErrorOr<Success>> AuthorizeAsync<TResource>(
        TResource resource,
        ResourceAction action,
        CancellationToken ct = default)
    {
        if (!CurrentUser.IsAuthenticated)
        {
            return Task.FromResult<ErrorOr<Success>>(Error.Unauthorized(
                "Authorization.Unauthenticated",
                "User is not authenticated."));
        }

        // Admins can do anything
        if (IsAdmin)
        {
            return Task.FromResult<ErrorOr<Success>>(Result.Success);
        }

        // Resource-specific authorization
        var result = resource switch
        {
            Note note => AuthorizeNote(note, action),
            _ => Result.Success
        };

        return Task.FromResult(result);
    }

    private ErrorOr<Success> AuthorizeNote(Note note, ResourceAction action)
    {
        var userId = CurrentUser.Id;

        return action switch
        {
            ResourceAction.Read when !note.CanRead(userId) =>
                Error.Forbidden(
                    "Note.AccessDenied",
                    "You don't have permission to read this note."),

            ResourceAction.Update or ResourceAction.Delete when !note.CanModify(userId) =>
                Error.Forbidden(
                    "Note.ModifyDenied",
                    "Only the creator can modify this note."),

            _ => Result.Success
        };
    }
}
