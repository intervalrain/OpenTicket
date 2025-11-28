using ErrorOr;
using OpenTicket.Application.Contracts.Identity;
using OpenTicket.Application.Contracts.Notes.Commands;
using OpenTicket.Application.Contracts.Notes.Events;
using OpenTicket.Application.Contracts.RateLimiting;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Ddd.Infrastructure;
using OpenTicket.Domain.Notes.Entities;

namespace OpenTicket.Application.Notes.Commands;

public sealed class CreateNoteCommandHandler : ICommandHandler<CreateNoteCommand, ErrorOr<CreateNoteCommandResult>>
{
    private readonly IRepository<Note> _noteRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IIntegrationEventPublisher _eventPublisher;
    private readonly IRateLimitService _rateLimitService;

    public CreateNoteCommandHandler(
        IRepository<Note> noteRepository,
        ICurrentUserProvider currentUserProvider,
        IIntegrationEventPublisher eventPublisher,
        IRateLimitService rateLimitService)
    {
        _noteRepository = noteRepository;
        _currentUserProvider = currentUserProvider;
        _eventPublisher = eventPublisher;
        _rateLimitService = rateLimitService;
    }

    public async Task<ErrorOr<CreateNoteCommandResult>> HandleAsync(CreateNoteCommand command, CancellationToken ct = default)
    {
        var currentUser = _currentUserProvider.CurrentUser;

        // Check rate limit for non-subscribers (3 notes per day)
        var rateLimitResult = await _rateLimitService.CheckRateLimitAsync(
            currentUser.Id,
            RateLimitedAction.CreateNote,
            ct);

        if (rateLimitResult.IsError)
        {
            return rateLimitResult.Errors;
        }

        // Create note with creator info
        var note = Note.Create(command.Title, command.Body, currentUser.Id);

        await _noteRepository.InsertAsync(note, ct);

        // Record the action for rate limiting
        await _rateLimitService.RecordActionAsync(currentUser.Id, RateLimitedAction.CreateNote, ct);

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
