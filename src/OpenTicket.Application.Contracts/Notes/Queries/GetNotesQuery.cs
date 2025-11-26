using OpenTicket.Application.Contracts.Notes.Dtos;
using OpenTicket.Ddd.Application.Cqrs;

namespace OpenTicket.Application.Contracts.Notes.Queries;

public record GetNotesQuery : IQuery<GetNotesQueryResult>;

public record GetNotesQueryResult(IReadOnlyList<NoteDto> Notes);
