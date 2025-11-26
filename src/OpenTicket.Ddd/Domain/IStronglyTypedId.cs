namespace OpenTicket.Ddd.Domain;

/// <summary>
/// Interface for strongly typed identifiers with Guid as the default underlying type.
/// Prevents primitive obsession by wrapping identifiers in type-safe structs.
/// </summary>
public interface IStronglyTypedId : IStronglyTypedId<Guid>
{
}

/// <summary>
/// Interface for strongly typed identifiers.
/// Prevents primitive obsession by wrapping identifiers in type-safe structs.
/// </summary>
/// <typeparam name="T">The underlying value type (usually Guid or string).</typeparam>
public interface IStronglyTypedId<out T>
{
    T Value { get; }
}

/// <summary>
/// Extension methods for strongly typed identifiers.
/// </summary>
public static class StronglyTypedIdExtensions
{
    /// <summary>
    /// Gets a short identifier (last 5 characters of the Guid) for display purposes.
    /// </summary>
    public static string ShortId(this IStronglyTypedId id) => id.Value.ToString()[^5..];
}