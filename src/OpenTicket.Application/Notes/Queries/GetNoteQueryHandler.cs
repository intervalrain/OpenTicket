using OpenTicket.Application.Contracts.Notes.Dtos;
using OpenTicket.Application.Contracts.Notes.Queries;
using OpenTicket.Ddd.Application;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Domain.Notes.Entities;

namespace OpenTicket.Application.Notes.Queries;

public sealed class GetNoteQueryHandler : IQueryHandler<GetNoteQuery, NoteDto?>
{
    private readonly IRepository<Note> _noteRepository;

    public GetNoteQueryHandler(IRepository<Note> noteRepository)
    {
        _noteRepository = noteRepository;
    }

    public async Task<NoteDto?> HandleAsync(GetNoteQuery query, CancellationToken ct = default)
    {
        var note = await _noteRepository.FindAsync(query.Id, ct);

        if (note is null)
            return null;

        return new NoteDto(note.Id, note.Title, note.Body, note.CreatedAt, note.UpdatedAt);
    }
}
