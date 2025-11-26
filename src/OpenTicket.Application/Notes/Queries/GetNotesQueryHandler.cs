using OpenTicket.Application.Contracts.Notes.Dtos;
using OpenTicket.Application.Contracts.Notes.Queries;
using OpenTicket.Ddd.Application;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Domain.Notes.Entities;

namespace OpenTicket.Application.Notes.Queries;

public sealed class GetNotesQueryHandler : IQueryHandler<GetNotesQuery, GetNotesQueryResult>
{
    private readonly IRepository<Note> _noteRepository;

    public GetNotesQueryHandler(IRepository<Note> noteRepository)
    {
        _noteRepository = noteRepository;
    }

    public async Task<GetNotesQueryResult> HandleAsync(GetNotesQuery query, CancellationToken ct = default)
    {
        var notes = await _noteRepository.ListAllAsync(ct);

        var dtos = notes
            .Select(n => new NoteDto(n.Id, n.Title, n.Body, n.CreatedAt, n.UpdatedAt))
            .ToList();

        return new GetNotesQueryResult(dtos);
    }
}
