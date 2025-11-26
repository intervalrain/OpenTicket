using OpenTicket.Application.Contracts.Notes.Commands;
using OpenTicket.Ddd.Application;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Domain.Notes.Entities;

namespace OpenTicket.Application.Notes.Commands;

public sealed class RemoveNoteCommandHandler : ICommandHandler<RemoveNoteCommand, bool>
{
    private readonly IRepository<Note> _noteRepository;

    public RemoveNoteCommandHandler(IRepository<Note> noteRepository)
    {
        _noteRepository = noteRepository;
    }

    public async Task<bool> HandleAsync(RemoveNoteCommand command, CancellationToken ct = default)
    {
        var note = await _noteRepository.FindAsync(command.Id, ct);

        if (note is null)
            return false;

        await _noteRepository.DeleteAsync(note, ct);

        return true;
    }
}
