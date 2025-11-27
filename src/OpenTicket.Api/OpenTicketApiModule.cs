using OpenTicket.Application;
using OpenTicket.Infrastructure.Database;
using OpenTicket.Infrastructure.Identity;
using OpenTicket.Infrastructure.Notification;
using Scalar.AspNetCore;

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

        // Register Identity (Mock for MVP)
        services.AddMockIdentity(configuration);

        // Register Notification (Console for MVP, use AddNotification for SMTP)
        services.AddNotificationConsole(configuration);

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

    /// <summary>
    /// Enables Scalar API documentation UI at /scalar/v1.
    /// A modern alternative to Swagger UI, compatible with .NET 9 OpenAPI.
    /// </summary>
    public static WebApplication UseScalarUI(this WebApplication app)
    {
        app.MapScalarApiReference(options => options
            .WithTitle("OpenTicket API")
            .WithTheme(ScalarTheme.DeepSpace)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));

        return app;
    }

    /// <summary>
    /// Enables Swagger UI at /swagger.
    /// Classic API documentation interface using NSwag.
    /// </summary>
    public static WebApplication UseSwaggerUI(this WebApplication app)
    {
        app.UseSwaggerUi(options =>
        {
            options.DocumentPath = "/openapi/v1.json";
            options.Path = "/swagger";
        });

        return app;
    }

    /// <summary>
    /// Enables ReDoc UI at /redoc.
    /// A clean, responsive API documentation interface.
    /// </summary>
    public static WebApplication UseReDocUI(this WebApplication app)
    {
        app.UseReDoc(options =>
        {
            options.DocumentPath = "/openapi/v1.json";
            options.Path = "/redoc";
        });

        return app;
    }
}