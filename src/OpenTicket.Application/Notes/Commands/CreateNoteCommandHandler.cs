using OpenTicket.Application.Contracts.Identity;
using OpenTicket.Application.Contracts.Notes.Commands;
using OpenTicket.Application.Contracts.Notes.Events;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Ddd.Infrastructure;
using OpenTicket.Domain.Notes.Entities;

namespace OpenTicket.Application.Notes.Commands;

public sealed class CreateNoteCommandHandler : ICommandHandler<CreateNoteCommand, CreateNoteCommandResult>
{
    private readonly IRepository<Note> _noteRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IIntegrationEventPublisher _eventPublisher;

    public CreateNoteCommandHandler(
        IRepository<Note> noteRepository,
        ICurrentUserProvider currentUserProvider,
        IIntegrationEventPublisher eventPublisher)
    {
        _noteRepository = noteRepository;
        _currentUserProvider = currentUserProvider;
        _eventPublisher = eventPublisher;
    }

    public async Task<CreateNoteCommandResult> HandleAsync(CreateNoteCommand command, CancellationToken ct = default)
    {
        var note = Note.Create(command.Title, command.Body);

        await _noteRepository.InsertAsync(note, ct);

        // Publish integration event for notification
        var @event = new NoteCreatedEvent
        {
            NoteId = note.Id,
            Title = note.Title,
            Body = note.Body,
            CreatedAt = note.CreatedAt,
            NotifyEmail = _currentUserProvider.GetRequiredEmail()
        };

        await _eventPublisher.PublishAsync(@event, ct);

        return new CreateNoteCommandResult(note.Id);
    }
}
