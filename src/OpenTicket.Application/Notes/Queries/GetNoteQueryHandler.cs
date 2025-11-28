using ErrorOr;
using OpenTicket.Application.Contracts.Authorization;
using OpenTicket.Application.Contracts.Notes.Dtos;
using OpenTicket.Application.Contracts.Notes.Queries;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Ddd.Infrastructure;
using OpenTicket.Domain.Notes.Entities;

namespace OpenTicket.Application.Notes.Queries;

public sealed class GetNoteQueryHandler : IQueryHandler<GetNoteQuery, ErrorOr<NoteDto>>
{
    private readonly IRepository<Note> _noteRepository;
    private readonly IAuthorizationService _authorizationService;

    public GetNoteQueryHandler(
        IRepository<Note> noteRepository,
        IAuthorizationService authorizationService)
    {
        _noteRepository = noteRepository;
        _authorizationService = authorizationService;
    }

    public async Task<ErrorOr<NoteDto>> HandleAsync(GetNoteQuery query, CancellationToken ct = default)
    {
        var note = await _noteRepository.FindAsync(query.Id, ct);

        if (note is null)
        {
            return Error.NotFound("Note.NotFound", "Note not found.");
        }

        // Check authorization - only creator, shared users, or admin can read
        var authResult = await _authorizationService.AuthorizeAsync(note, ResourceAction.Read, ct);
        if (authResult.IsError)
        {
            return authResult.Errors;
        }

        return new NoteDto(
            note.Id,
            note.Title,
            note.Body,
            note.CreatedAt,
            note.UpdatedAt,
            note.CreatorId.Value,
            note.SharedWith.Select(u => u.Value).ToList());
    }
}
