using ErrorOr;
using OpenTicket.Ddd.Application.Cqrs;

namespace OpenTicket.Application.Contracts.Notes.Commands;

public record UpdateNoteCommand(Guid Id, string Title, string Body) : ICommand<ErrorOr<Updated>>;
