using OpenTicket.Ddd.Application.IntegrationEvents;

namespace OpenTicket.Application.Contracts.Notes.Events;

/// <summary>
/// Integration event raised when a note is created.
/// </summary>
public record NoteCreatedEvent : IntegrationEvent
{
    /// <summary>
    /// The note ID.
    /// </summary>
    public Guid NoteId { get; init; }

    /// <summary>
    /// The note title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The note body.
    /// </summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// When the note was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Email address to notify.
    /// </summary>
    public string NotifyEmail { get; init; } = string.Empty;

    public override string AggregateId => NoteId.ToString();
}
