using OpenTicket.Application.Contracts.Notes.Commands;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Ddd.Infrastructure;
using OpenTicket.Domain.Notes.Entities;

namespace OpenTicket.Application.Notes.Commands;

public sealed class UpdateNoteCommandHandler : ICommandHandler<UpdateNoteCommand, bool>
{
    private readonly IRepository<Note> _noteRepository;

    public UpdateNoteCommandHandler(IRepository<Note> noteRepository)
    {
        _noteRepository = noteRepository;
    }

    public async Task<bool> HandleAsync(UpdateNoteCommand command, CancellationToken ct = default)
    {
        var note = await _noteRepository.FindAsync(command.Id, ct);

        if (note is null)
            return false;

        note.Update(command.Title, command.Body);

        await _noteRepository.UpdateAsync(note, ct);

        return true;
    }
}
