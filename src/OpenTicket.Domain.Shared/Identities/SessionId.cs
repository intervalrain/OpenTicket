using OpenTicket.Ddd.Domain;

namespace OpenTicket.Domain.Shared.Identities;

public readonly record struct SessionId(Guid Value) : IStronglyTypedId
{
    public static SessionId New() => new(Guid.NewGuid());
    public static SessionId From(Guid value) => new(value);
    public override string ToString() => this.ShortId();
}