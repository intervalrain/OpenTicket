using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using OpenTicket.Api.Services;
using OpenTicket.Application;
using OpenTicket.Infrastructure.Cache;
using OpenTicket.Infrastructure.Cache.Abstractions;
using OpenTicket.Infrastructure.Database;
using OpenTicket.Infrastructure.Identity;
using OpenTicket.Infrastructure.MessageBroker;
using OpenTicket.Infrastructure.MessageBroker.Abstractions;
using OpenTicket.Infrastructure.Notification;
using OpenTicket.Infrastructure.Notification.Abstractions;
using Scalar.AspNetCore;

namespace OpenTicket.Api;

public static class OpenTicketApiModule
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        // Add controllers
        services.AddControllers();

        // Add OpenAPI/Swagger with JWT Bearer authentication
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                // Add JWT Bearer security scheme
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = JwtBearerDefaults.AuthenticationScheme,
                    BearerFormat = "JWT",
                    Description = "Enter your JWT token. You can get one by logging in via /api/auth/login/{provider}"
                };

                // Apply security requirement globally
                document.SecurityRequirements.Add(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });

                return Task.CompletedTask;
            });
        });

        // Register Application layer (CQRS, handlers, settings)
        services.AddApplication(configuration);

        // Register Persistence (InMemory for MVP)
        services.AddPersistence(DatabaseOption.InMemory);

        // Register Cache (InMemory for MVP, Redis/NATS KV for production)
        services.AddCache(configuration, CacheProvider.InMemory);

        // Register Identity (OAuth with JWT for production, Mock fallback for testing)
        services.AddOAuthIdentity(configuration);

        // Register Message Broker (InMemory for MVP, NATS/Redis for production)
        services.AddMessageBroker(configuration, MessageBrokerOption.InMemory);

        // Register Notification channels (mode is controlled by appsettings)
        services.AddNotification(configuration, NotificationChannel.Email);

        // Register background service for outbox processing
        services.AddHostedService<OutboxProcessorHostedService>();

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

        // Enable authentication and authorization middleware
        app.UseAuthentication();
        app.UseAuthorization();

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