using OpenTicket.Ddd.Application.Cqrs;

namespace OpenTicket.Application.Contracts.Notes.Commands;

public record CreateNoteCommand(string Title, string Body) : ICommand<CreateNoteCommandResult>;

public record CreateNoteCommandResult(Guid Id);
