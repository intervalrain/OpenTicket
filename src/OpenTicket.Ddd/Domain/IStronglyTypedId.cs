namespace OpenTicket.Ddd.Domain;

/// <summary>
/// Interface for strongly typed identifiers.
/// Prevents primitive obsession by wrapping identifiers in type-safe structs.
/// </summary>
/// <typeparam name="T">The underlying value type (usually Guid or string).</typeparam>
public interface IStronglyTypedId<out T>
{
    T Value { get; }
}