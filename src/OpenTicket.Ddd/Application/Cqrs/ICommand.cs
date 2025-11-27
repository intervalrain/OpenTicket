namespace OpenTicket.Ddd.Application.Cqrs;

/// <summary>
/// Marker interface for commands that return a result.
/// Commands represent intentions to change state.
/// </summary>
/// <typeparam name="TResult">The type of result returned by the command.</typeparam>
public interface ICommand<TResult> { }

/// <summary>
/// Marker interface for commands that don't return a result.
/// </summary>
public interface ICommand : ICommand<Unit> { }