using OpenTicket.Application.Contracts.Notes.Commands;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Ddd.Infrastructure;
using OpenTicket.Domain.Notes.Entities;

namespace OpenTicket.Application.Notes.Commands;

public sealed class PatchNoteCommandHandler : ICommandHandler<PatchNoteCommand, bool>
{
    private readonly IRepository<Note> _noteRepository;

    public PatchNoteCommandHandler(IRepository<Note> noteRepository)
    {
        _noteRepository = noteRepository;
    }

    public async Task<bool> HandleAsync(PatchNoteCommand command, CancellationToken ct = default)
    {
        var note = await _noteRepository.FindAsync(command.Id, ct);

        if (note is null)
            return false;

        // Apply partial update - only update fields that are provided
        var newTitle = command.Title ?? note.Title;
        var newBody = command.Body ?? note.Body;

        note.Update(newTitle, newBody);

        await _noteRepository.UpdateAsync(note, ct);

        return true;
    }
}