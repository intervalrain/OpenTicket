using OpenTicket.Ddd.Domain;

namespace OpenTicket.Domain.Notes.Entities;

/// <summary>
/// A simple Note aggregate for demonstrating CQRS pattern.
/// </summary>
public class Note : AggregateRoot
{
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Note() { } // EF Core

    public static Note Create(string title, string body)
    {
        return new Note
        {
            Id = Guid.NewGuid(),
            Title = title,
            Body = body,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string title, string body)
    {
        Title = title;
        Body = body;
        UpdatedAt = DateTime.UtcNow;
    }
}
