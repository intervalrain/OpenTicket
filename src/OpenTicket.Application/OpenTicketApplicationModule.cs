using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTicket.Application.Tickets.Settings;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Ddd.Application.Cqrs.Behaviors;

namespace OpenTicket.Application;

public static class OpenTicketApplicationModule
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TicketSettings>(configuration.GetSection(TicketSettings.SectionName));

        // Register CQRS dispatcher and handlers
        services.AddCqrs(Assembly.GetExecutingAssembly());

        // Register pipeline behaviors
        services.AddPipelineBehavior(typeof(LoggingBehavior<,>));

        return services;
    }
}
