using ErrorOr;
using OpenTicket.Ddd.Application.Cqrs;

namespace OpenTicket.Application.Contracts.Notes.Commands;

public record RemoveNoteCommand(Guid Id) : ICommand<ErrorOr<Deleted>>;
