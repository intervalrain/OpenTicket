using OpenTicket.Ddd.Application.Cqrs;

namespace OpenTicket.Application.Contracts.Notes.Commands;

/// <summary>
/// Command for partially updating a note.
/// Null values indicate fields that should not be changed.
/// </summary>
public record PatchNoteCommand(Guid Id, string? Title, string? Body) : ICommand<bool>;
