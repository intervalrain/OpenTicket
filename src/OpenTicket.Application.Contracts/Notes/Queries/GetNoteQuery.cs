using ErrorOr;
using OpenTicket.Application.Contracts.Notes.Dtos;
using OpenTicket.Ddd.Application.Cqrs;

namespace OpenTicket.Application.Contracts.Notes.Queries;

public record GetNoteQuery(Guid Id) : IQuery<ErrorOr<NoteDto>>;
