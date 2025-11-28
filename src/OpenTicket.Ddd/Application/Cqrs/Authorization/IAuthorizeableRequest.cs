namespace OpenTicket.Ddd.Application.Cqrs.Authorization;

/// <summary>
/// Marker interface for requests that require authorization.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IAuthorizeableRequest<TResponse>;

/// <summary>
/// Command that requires authorization checks.
/// Use this instead of ICommand when you want to apply [Authorize] attributes.
/// </summary>
/// <typeparam name="TResult">The result type.</typeparam>
/// <example>
/// <code>
/// [Authorize(Roles = "Admin,User")]
/// public record CreateNoteCommand(string Title) : IAuthorizeableCommand&lt;ErrorOr&lt;NoteId&gt;&gt;;
/// </code>
/// </example>
public interface IAuthorizeableCommand<TResult> : ICommand<TResult>, IAuthorizeableRequest<TResult>;

/// <summary>
/// Command without result that requires authorization checks.
/// </summary>
public interface IAuthorizeableCommand : IAuthorizeableCommand<Unit>;

/// <summary>
/// Query that requires authorization checks.
/// Use this instead of IQuery when you want to apply [Authorize] attributes.
/// </summary>
/// <typeparam name="TResult">The result type.</typeparam>
/// <example>
/// <code>
/// [Authorize(Roles = "User")]
/// public record GetNoteQuery(Guid Id) : IAuthorizeableQuery&lt;ErrorOr&lt;NoteResponse&gt;&gt;;
/// </code>
/// </example>
public interface IAuthorizeableQuery<TResult> : IQuery<TResult>, IAuthorizeableRequest<TResult>;
