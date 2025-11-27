using OpenTicket.Application.Contracts.Identity;
using OpenTicket.Application.Contracts.Notes.Commands;
using OpenTicket.Application.Contracts.Notes.Events;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Ddd.Infrastructure;
using OpenTicket.Domain.Notes.Entities;

namespace OpenTicket.Application.Notes.Commands;

public sealed class UpdateNoteCommandHandler : ICommandHandler<UpdateNoteCommand, bool>
{
    private readonly IRepository<Note> _noteRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IIntegrationEventPublisher _eventPublisher;

    public UpdateNoteCommandHandler(
        IRepository<Note> noteRepository,
        ICurrentUserProvider currentUserProvider,
        IIntegrationEventPublisher eventPublisher)
    {
        _noteRepository = noteRepository;
        _currentUserProvider = currentUserProvider;
        _eventPublisher = eventPublisher;
    }

    public async Task<bool> HandleAsync(UpdateNoteCommand command, CancellationToken ct = default)
    {
        var note = await _noteRepository.FindAsync(command.Id, ct);

        if (note is null)
            return false;

        var previousTitle = note.Title;
        var previousBody = note.Body;

        note.Update(command.Title, command.Body);

        await _noteRepository.UpdateAsync(note, ct);

        // Publish integration event for notification
        var @event = new NoteUpdatedEvent
        {
            NoteId = note.Id,
            PreviousTitle = previousTitle,
            NewTitle = note.Title,
            PreviousBody = previousBody,
            NewBody = note.Body,
            UpdatedAt = note.UpdatedAt ?? DateTime.UtcNow,
            NotifyEmail = _currentUserProvider.GetRequiredEmail()
        };

        await _eventPublisher.PublishAsync(@event, ct);

        return true;
    }
}
