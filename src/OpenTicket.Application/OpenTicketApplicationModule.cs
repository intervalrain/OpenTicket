using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTicket.Application.Tickets.Settings;

namespace OpenTicket.Application;

public static class OpenTicketApplicationModule
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TicketSettings>(configuration.GetSection(TicketSettings.SectionName));

        return services;
    }
}
