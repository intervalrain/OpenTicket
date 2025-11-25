using System.Reflection;

namespace OpenTicket.Ddd.Domain;

/// <summary>
/// Base class for smart enumerations. Provides type-safe enum alternatives
/// with behavior and additional data.
/// </summary>
/// <typeparam name="TEnum">The enumeration type.</typeparam>
public abstract class Enumeration<TEnum> : IEquatable<Enumeration<TEnum>>
    where TEnum : Enumeration<TEnum>
{
    private static readonly Lazy<Dictionary<int, TEnum>> AllItems = new(GetAllItems);
    private static readonly Lazy<Dictionary<string, TEnum>> AllItemsByName = new(
        () => AllItems.Value.Values.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase));

    public int Id { get; }
    public string Name { get; }

    protected Enumeration(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public static IReadOnlyCollection<TEnum> GetAll() => AllItems.Value.Values.ToList().AsReadOnly();

    public static TEnum? FromId(int id)
    {
        return AllItems.Value.TryGetValue(id, out var item) ? item : null;
    }

    public static TEnum? FromName(string name)
    {
        return AllItemsByName.Value.TryGetValue(name, out var item) ? item : null;
    }

    public bool Equals(Enumeration<TEnum>? other)
    {
        if (other is null) return false;
        return GetType() == other.GetType() && Id == other.Id;
    }

    public override bool Equals(object? obj) => obj is Enumeration<TEnum> other && Equals(other);

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => Name;

    public static bool operator ==(Enumeration<TEnum>? left, Enumeration<TEnum>? right) => Equals(left, right);

    public static bool operator !=(Enumeration<TEnum>? left, Enumeration<TEnum>? right) => !Equals(left, right);

    private static Dictionary<int, TEnum> GetAllItems()
    {
        return typeof(TEnum)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.FieldType == typeof(TEnum))
            .Select(f => (TEnum)f.GetValue(null)!)
            .ToDictionary(item => item.Id);
    }
}