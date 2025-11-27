using OpenTicket.Ddd.Application.IntegrationEvents;

namespace OpenTicket.Application.Contracts.Notes.Events;

/// <summary>
/// Integration event raised when a note is updated (full update).
/// </summary>
public record NoteUpdatedEvent : IntegrationEvent
{
    /// <summary>
    /// The note ID.
    /// </summary>
    public Guid NoteId { get; init; }

    /// <summary>
    /// The previous title.
    /// </summary>
    public string PreviousTitle { get; init; } = string.Empty;

    /// <summary>
    /// The new title.
    /// </summary>
    public string NewTitle { get; init; } = string.Empty;

    /// <summary>
    /// The previous body.
    /// </summary>
    public string PreviousBody { get; init; } = string.Empty;

    /// <summary>
    /// The new body.
    /// </summary>
    public string NewBody { get; init; } = string.Empty;

    /// <summary>
    /// When the note was updated.
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Email address to notify.
    /// </summary>
    public string NotifyEmail { get; init; } = string.Empty;

    public override string AggregateId => NoteId.ToString();
}
