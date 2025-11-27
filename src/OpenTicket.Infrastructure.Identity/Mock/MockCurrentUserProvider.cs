using Microsoft.Extensions.Options;
using OpenTicket.Application.Contracts.Identity;
using OpenTicket.Domain.Shared.Identities;

namespace OpenTicket.Infrastructure.Identity.Mock;

/// <summary>
/// Mock implementation of ICurrentUserProvider for development and testing.
/// Provides a configurable static user identity.
/// </summary>
public sealed class MockCurrentUserProvider : ICurrentUserProvider
{
    private readonly CurrentUser _currentUser;

    public MockCurrentUserProvider(IOptions<MockUserOptions> options)
    {
        var opt = options.Value;
        _currentUser = new CurrentUser
        {
            Id = UserId.From(opt.UserId),
            Email = opt.Email,
            Name = opt.Name,
            IsAuthenticated = true,
            Provider = "Mock",
            Roles = opt.Roles
        };
    }

    public CurrentUser CurrentUser => _currentUser;
}
