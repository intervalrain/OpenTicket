using OpenTicket.Ddd.Domain;
using OpenTicket.Domain.Shared.Identities;

namespace OpenTicket.Domain.Notes.Entities;

/// <summary>
/// A simple Note aggregate for demonstrating CQRS pattern.
/// </summary>
public class Note : AggregateRoot
{
    private readonly List<UserId> _sharedWith = [];

    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>
    /// The user who created this note.
    /// </summary>
    public UserId CreatorId { get; private set; }

    /// <summary>
    /// List of user IDs this note is shared with.
    /// </summary>
    public IReadOnlyList<UserId> SharedWith => _sharedWith.AsReadOnly();

    private Note() { } // EF Core

    public static Note Create(string title, string body, UserId creatorId)
    {
        return new Note
        {
            Id = Guid.NewGuid(),
            Title = title,
            Body = body,
            CreatorId = creatorId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string title, string body)
    {
        Title = title;
        Body = body;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Shares this note with a user.
    /// </summary>
    public void ShareWith(UserId userId)
    {
        if (!_sharedWith.Contains(userId) && userId != CreatorId)
        {
            _sharedWith.Add(userId);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Removes sharing with a user.
    /// </summary>
    public void UnshareWith(UserId userId)
    {
        if (_sharedWith.Remove(userId))
        {
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Checks if the note is shared with a user.
    /// </summary>
    public bool IsSharedWith(UserId userId) => _sharedWith.Contains(userId);

    /// <summary>
    /// Checks if a user can read this note.
    /// A user can read a note if they are the creator or the note is shared with them.
    /// </summary>
    public bool CanRead(UserId userId) => CreatorId == userId || IsSharedWith(userId);

    /// <summary>
    /// Checks if a user can modify (update/delete) this note.
    /// Only the creator can modify a note.
    /// </summary>
    public bool CanModify(UserId userId) => CreatorId == userId;
}
