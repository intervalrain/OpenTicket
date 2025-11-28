using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTicket.Application.Authorization;
using OpenTicket.Application.Contracts.Authorization;
using OpenTicket.Application.Contracts.RateLimiting;
using OpenTicket.Application.RateLimiting;
using OpenTicket.Application.Tickets.Settings;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Ddd.Application.Cqrs.Authorization;

namespace OpenTicket.Application;

public static class OpenTicketApplicationModule
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TicketSettings>(configuration.GetSection(TicketSettings.SectionName));

        var assembly = Assembly.GetExecutingAssembly();

        // Register CQRS dispatcher and handlers
        services.AddCqrs(assembly);

        // Register validators
        services.AddValidatorsFromAssembly(assembly);

        // Register standard pipeline behaviors (Audit -> Logging -> Validation -> Transaction)
        services.AddStandardBehaviors();

        // Register integration events infrastructure
        services.AddIntegrationEvents();

        // Register authorization and rate limiting services
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IRequestAuthorizationService, RequestAuthorizationService>();
        services.AddScoped<IRateLimitService, RateLimitService>();

        return services;
    }
}
