using OpenTicket.Ddd.Application.IntegrationEvents;

namespace OpenTicket.Application.Contracts.Notes.Events;

/// <summary>
/// Integration event raised when a note is patched (partial update).
/// </summary>
public record NotePatchedEvent : IntegrationEvent
{
    /// <summary>
    /// The note ID.
    /// </summary>
    public Guid NoteId { get; init; }

    /// <summary>
    /// Fields that were patched.
    /// </summary>
    public IReadOnlyList<string> PatchedFields { get; init; } = [];

    /// <summary>
    /// The new title (if patched).
    /// </summary>
    public string? NewTitle { get; init; }

    /// <summary>
    /// The new body (if patched).
    /// </summary>
    public string? NewBody { get; init; }

    /// <summary>
    /// When the note was patched.
    /// </summary>
    public DateTime PatchedAt { get; init; }

    /// <summary>
    /// Email address to notify.
    /// </summary>
    public string NotifyEmail { get; init; } = string.Empty;

    public override string AggregateId => NoteId.ToString();
}
