using ErrorOr;
using OpenTicket.Application.Contracts.Authorization;
using OpenTicket.Application.Contracts.Notes.Commands;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Ddd.Infrastructure;
using OpenTicket.Domain.Notes.Entities;

namespace OpenTicket.Application.Notes.Commands;

public sealed class RemoveNoteCommandHandler : ICommandHandler<RemoveNoteCommand, ErrorOr<Deleted>>
{
    private readonly IRepository<Note> _noteRepository;
    private readonly IAuthorizationService _authorizationService;

    public RemoveNoteCommandHandler(
        IRepository<Note> noteRepository,
        IAuthorizationService authorizationService)
    {
        _noteRepository = noteRepository;
        _authorizationService = authorizationService;
    }

    public async Task<ErrorOr<Deleted>> HandleAsync(RemoveNoteCommand command, CancellationToken ct = default)
    {
        var note = await _noteRepository.FindAsync(command.Id, ct);

        if (note is null)
        {
            return Error.NotFound("Note.NotFound", "Note not found.");
        }

        // Check authorization - only creator or admin can delete
        var authResult = await _authorizationService.AuthorizeAsync(note, ResourceAction.Delete, ct);
        if (authResult.IsError)
        {
            return authResult.Errors;
        }

        await _noteRepository.DeleteAsync(note, ct);

        return Result.Deleted;
    }
}
