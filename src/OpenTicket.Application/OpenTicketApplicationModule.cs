using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTicket.Application.Tickets.Settings;
using OpenTicket.Ddd.Application.Cqrs;

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

        return services;
    }
}
