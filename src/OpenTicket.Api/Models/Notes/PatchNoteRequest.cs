namespace OpenTicket.Api.Models.Notes;

/// <summary>
/// Request model for partially updating a note.
/// </summary>
public record PatchNoteRequest
{
    /// <summary>
    /// The new title (optional).
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// The new body (optional).
    /// </summary>
    public string? Body { get; init; }
}