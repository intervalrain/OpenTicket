using System.Collections.Concurrent;

namespace OpenTicket.Ddd.Application.IntegrationEvents.Internal;

/// <summary>
/// Registry for mapping event type names to their CLR types.
/// Used for deserializing integration events from the message broker.
/// </summary>
public sealed class IntegrationEventTypeRegistry
{
    private readonly ConcurrentDictionary<string, Type> _typeMap = new();

    /// <summary>
    /// Registers an event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type to register.</typeparam>
    public void Register<TEvent>() where TEvent : IIntegrationEvent
    {
        var type = typeof(TEvent);
        _typeMap.TryAdd(type.Name, type);
    }

    /// <summary>
    /// Registers an event type with a custom name.
    /// </summary>
    /// <typeparam name="TEvent">The event type to register.</typeparam>
    /// <param name="eventTypeName">The name to register the type under.</param>
    public void Register<TEvent>(string eventTypeName) where TEvent : IIntegrationEvent
    {
        _typeMap.TryAdd(eventTypeName, typeof(TEvent));
    }

    /// <summary>
    /// Gets the CLR type for an event type name.
    /// </summary>
    /// <param name="eventTypeName">The event type name.</param>
    /// <returns>The CLR type, or null if not registered.</returns>
    public Type? GetType(string eventTypeName)
    {
        _typeMap.TryGetValue(eventTypeName, out var type);
        return type;
    }

    /// <summary>
    /// Checks if an event type is registered.
    /// </summary>
    /// <param name="eventTypeName">The event type name.</param>
    /// <returns>True if the type is registered.</returns>
    public bool IsRegistered(string eventTypeName)
        => _typeMap.ContainsKey(eventTypeName);

    /// <summary>
    /// Gets all registered event types.
    /// </summary>
    public IReadOnlyDictionary<string, Type> GetAllTypes()
        => _typeMap;
}
