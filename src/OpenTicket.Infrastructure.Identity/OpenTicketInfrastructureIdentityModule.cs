using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTicket.Application.Contracts.Identity;
using OpenTicket.Infrastructure.Identity.Mock;

namespace OpenTicket.Infrastructure.Identity;

/// <summary>
/// Module for registering identity services.
/// </summary>
public static class OpenTicketInfrastructureIdentityModule
{
    /// <summary>
    /// Adds mock identity provider for development and testing.
    /// </summary>
    public static IServiceCollection AddMockIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MockUserOptions>(
            configuration.GetSection(MockUserOptions.SectionName));

        services.AddScoped<ICurrentUserProvider, MockCurrentUserProvider>();

        return services;
    }

    /// <summary>
    /// Adds mock identity provider with custom configuration.
    /// </summary>
    public static IServiceCollection AddMockIdentity(
        this IServiceCollection services,
        Action<MockUserOptions> configure)
    {
        services.Configure(configure);
        services.AddScoped<ICurrentUserProvider, MockCurrentUserProvider>();

        return services;
    }

    // Future: AddGoogleIdentity, AddGitHubIdentity, AddAppleIdentity
}
