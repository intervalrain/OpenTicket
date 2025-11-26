using OpenTicket.Application;
using OpenTicket.Infrastructure.Database;

namespace OpenTicket.Api;

public static class OpenTicketApiModule
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        // Add controllers
        services.AddControllers();

        // Add OpenAPI/Swagger
        services.AddOpenApi();

        // Register Application layer (CQRS, handlers, settings)
        services.AddApplication(configuration);

        // Register Persistence (InMemory for MVP)
        services.AddPersistence(DatabaseOption.InMemory);

        return services;
    }

    public static WebApplication UseApi(this WebApplication app)
    {
        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();
        app.MapControllers();

        return app;
    }
}