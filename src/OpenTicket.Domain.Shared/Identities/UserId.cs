using OpenTicket.Ddd.Domain;

namespace OpenTicket.Domain.Shared.Identities;

public readonly record struct UserId(Guid Value) : IStronglyTypedId
{
    public static UserId New() => new(Guid.NewGuid());
    public static UserId From(Guid value) => new(value);
    public override string ToString() => this.ShortId();
}