using OpenTicket.Application.Contracts.Notes.Commands;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Ddd.Infrastructure;
using OpenTicket.Domain.Notes.Entities;

namespace OpenTicket.Application.Notes.Commands;

public sealed class CreateNoteCommandHandler : ICommandHandler<CreateNoteCommand, CreateNoteCommandResult>
{
    private readonly IRepository<Note> _noteRepository;

    public CreateNoteCommandHandler(IRepository<Note> noteRepository)
    {
        _noteRepository = noteRepository;
    }

    public async Task<CreateNoteCommandResult> HandleAsync(CreateNoteCommand command, CancellationToken ct = default)
    {
        var note = Note.Create(command.Title, command.Body);

        await _noteRepository.InsertAsync(note, ct);

        return new CreateNoteCommandResult(note.Id);
    }
}
