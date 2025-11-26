using OpenTicket.Ddd.Domain;

namespace OpenTicket.Domain.Shared.Identities;

public readonly record struct AreaId(Guid Value) : IStronglyTypedId
{
    public static AreaId New() => new(Guid.NewGuid());
    public static AreaId From(Guid value) => new(value);
    public override string ToString() => this.ShortId();
}