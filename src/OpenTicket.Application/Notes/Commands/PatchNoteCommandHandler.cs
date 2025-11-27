using OpenTicket.Application.Contracts.Identity;
using OpenTicket.Application.Contracts.Notes.Commands;
using OpenTicket.Application.Contracts.Notes.Events;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Ddd.Infrastructure;
using OpenTicket.Domain.Notes.Entities;

namespace OpenTicket.Application.Notes.Commands;

public sealed class PatchNoteCommandHandler : ICommandHandler<PatchNoteCommand, bool>
{
    private readonly IRepository<Note> _noteRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IIntegrationEventPublisher _eventPublisher;

    public PatchNoteCommandHandler(
        IRepository<Note> noteRepository,
        ICurrentUserProvider currentUserProvider,
        IIntegrationEventPublisher eventPublisher)
    {
        _noteRepository = noteRepository;
        _currentUserProvider = currentUserProvider;
        _eventPublisher = eventPublisher;
    }

    public async Task<bool> HandleAsync(PatchNoteCommand command, CancellationToken ct = default)
    {
        var note = await _noteRepository.FindAsync(command.Id, ct);

        if (note is null)
            return false;

        // Track patched fields
        var patchedFields = new List<string>();
        if (command.Title is not null) patchedFields.Add("Title");
        if (command.Body is not null) patchedFields.Add("Body");

        // Apply partial update - only update fields that are provided
        var newTitle = command.Title ?? note.Title;
        var newBody = command.Body ?? note.Body;

        note.Update(newTitle, newBody);

        await _noteRepository.UpdateAsync(note, ct);

        // Publish integration event for notification
        var @event = new NotePatchedEvent
        {
            NoteId = note.Id,
            PatchedFields = patchedFields,
            NewTitle = command.Title,
            NewBody = command.Body,
            PatchedAt = note.UpdatedAt ?? DateTime.UtcNow,
            NotifyEmail = _currentUserProvider.GetRequiredEmail()
        };

        await _eventPublisher.PublishAsync(@event, ct);

        return true;
    }
}