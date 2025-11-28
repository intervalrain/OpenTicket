namespace OpenTicket.Application.Contracts.Notes.Dtos;

public record NoteDto(
    Guid Id,
    string Title,
    string Body,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid CreatorId,
    IReadOnlyList<Guid> SharedWith);
