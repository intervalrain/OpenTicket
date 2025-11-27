using System.Text.Json;

namespace OpenTicket.Ddd.Application.IntegrationEvents.Internal;

/// <summary>
/// Serializes and deserializes integration events.
/// </summary>
public static class IntegrationEventSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Serializes an integration event to JSON.
    /// </summary>
    public static string Serialize<TEvent>(TEvent @event) where TEvent : IIntegrationEvent
        => JsonSerializer.Serialize(@event, @event.GetType(), Options);

    /// <summary>
    /// Deserializes an integration event from JSON.
    /// </summary>
    public static TEvent? Deserialize<TEvent>(string json) where TEvent : IIntegrationEvent
        => JsonSerializer.Deserialize<TEvent>(json, Options);

    /// <summary>
    /// Deserializes an integration event from JSON with a runtime type.
    /// </summary>
    public static object? Deserialize(string json, Type eventType)
        => JsonSerializer.Deserialize(json, eventType, Options);
}
