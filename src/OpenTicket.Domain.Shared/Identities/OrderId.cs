using OpenTicket.Ddd.Domain;

namespace OpenTicket.Domain.Shared.Identities;

public readonly record struct OrderId(Guid Value) : IStronglyTypedId
{
    public static OrderId New() => new(Guid.NewGuid());
    public static OrderId From(Guid value) => new(value);
    public override string ToString() => this.ShortId();
}