namespace OpenTicket.Ddd.Application.Cqrs.Authorization;

/// <summary>
/// Marks a request as requiring authorization.
/// Can be applied multiple times to require multiple authorization conditions.
/// </summary>
/// <example>
/// <code>
/// [Authorize(Roles = "Admin,User")]
/// [Authorize(Permissions = "Note.Create")]
/// public class CreateNoteCommand : ICommand&lt;ErrorOr&lt;NoteId&gt;&gt;, IAuthorizeableRequest&lt;ErrorOr&lt;NoteId&gt;&gt;
/// {
///     public Guid? UserId { get; init; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class AuthorizeAttribute : Attribute
{
    /// <summary>
    /// Comma-separated list of required permissions.
    /// User must have ALL specified permissions.
    /// </summary>
    /// <example>"Note.Create,Note.Update"</example>
    public string? Permissions { get; set; }

    /// <summary>
    /// Comma-separated list of required roles.
    /// User must have at least ONE of the specified roles.
    /// </summary>
    /// <example>"Admin,User"</example>
    public string? Roles { get; set; }

    /// <summary>
    /// Comma-separated list of policy names to evaluate.
    /// All policies must pass.
    /// </summary>
    /// <example>"ResourceOwner,ActiveSubscription"</example>
    public string? Policies { get; set; }
}
