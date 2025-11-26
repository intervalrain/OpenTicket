namespace OpenTicket.Ddd.Application.Cqrs;

/// <summary>
/// Marker interface for queries.
/// Queries represent read operations and should not change state.
/// </summary>
/// <typeparam name="TResult">The type of result returned by the query.</typeparam>
public interface IQuery<TResult> { }
