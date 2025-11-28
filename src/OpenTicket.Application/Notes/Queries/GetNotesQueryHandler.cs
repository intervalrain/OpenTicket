using OpenTicket.Application.Contracts.Identity;
using OpenTicket.Application.Contracts.Notes.Dtos;
using OpenTicket.Application.Contracts.Notes.Queries;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Ddd.Infrastructure;
using OpenTicket.Domain.Notes.Entities;

namespace OpenTicket.Application.Notes.Queries;

public sealed class GetNotesQueryHandler : IQueryHandler<GetNotesQuery, GetNotesQueryResult>
{
    private readonly IRepository<Note> _noteRepository;
    private readonly ICurrentUserProvider _currentUserProvider;

    public GetNotesQueryHandler(
        IRepository<Note> noteRepository,
        ICurrentUserProvider currentUserProvider)
    {
        _noteRepository = noteRepository;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<GetNotesQueryResult> HandleAsync(GetNotesQuery query, CancellationToken ct = default)
    {
        var currentUser = _currentUserProvider.CurrentUser;
        var notes = await _noteRepository.ListAllAsync(ct);

        // Filter notes: user can see their own notes or notes shared with them
        // Admins can see all notes
        var visibleNotes = currentUser.IsAdmin
            ? notes
            : notes.Where(n => n.CanRead(currentUser.Id));

        var dtos = visibleNotes
            .Select(n => new NoteDto(
                n.Id,
                n.Title,
                n.Body,
                n.CreatedAt,
                n.UpdatedAt,
                n.CreatorId.Value,
                n.SharedWith.Select(u => u.Value).ToList()))
            .ToList();

        return new GetNotesQueryResult(dtos);
    }
}
