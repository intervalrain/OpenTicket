using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTicket.Application.Contracts.Notes.Events;
using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Infrastructure.Notification.Abstractions;
using OpenTicket.Infrastructure.Notification.Handlers;
using OpenTicket.Infrastructure.Notification.Internal;
using OpenTicket.Infrastructure.Notification.Smtp;

namespace OpenTicket.Infrastructure.Notification;

/// <summary>
/// Module for registering notification services.
/// </summary>
public static class OpenTicketInfrastructureNotificationModule
{
    /// <summary>
    /// Adds notification services with SMTP email support.
    /// </summary>
    public static IServiceCollection AddNotification(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register options
        services.Configure<NotificationOptions>(
            configuration.GetSection(NotificationOptions.SectionName));
        services.Configure<SmtpOptions>(
            configuration.GetSection(SmtpOptions.SectionName));

        // Register SMTP sender
        services.AddSingleton<INotificationSender, SmtpNotificationSender>();

        // Register notification service
        services.AddSingleton<INotificationService, NotificationService>();

        // Register event handlers
        services.AddScoped<IIntegrationEventHandler<NoteCreatedEvent>, NoteCreatedEventHandler>();
        services.AddScoped<IIntegrationEventHandler<NoteUpdatedEvent>, NoteUpdatedEventHandler>();
        services.AddScoped<IIntegrationEventHandler<NotePatchedEvent>, NotePatchedEventHandler>();

        return services;
    }

    /// <summary>
    /// Adds notification services with console output (for MVP/testing).
    /// </summary>
    public static IServiceCollection AddNotificationConsole(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register options
        services.Configure<NotificationOptions>(
            configuration.GetSection(NotificationOptions.SectionName));

        // Register console sender for Email channel
        services.AddSingleton<INotificationSender>(sp =>
            new ConsoleNotificationSender(
                NotificationChannel.Email,
                sp.GetRequiredService<ILogger<ConsoleNotificationSender>>()));

        // Register notification service
        services.AddSingleton<INotificationService, NotificationService>();

        // Register event handlers
        services.AddScoped<IIntegrationEventHandler<NoteCreatedEvent>, NoteCreatedEventHandler>();
        services.AddScoped<IIntegrationEventHandler<NoteUpdatedEvent>, NoteUpdatedEventHandler>();
        services.AddScoped<IIntegrationEventHandler<NotePatchedEvent>, NotePatchedEventHandler>();

        return services;
    }

    /// <summary>
    /// Adds notification services with custom configuration.
    /// </summary>
    public static IServiceCollection AddNotification(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<NotificationOptions> configure)
    {
        services.AddNotification(configuration);
        services.PostConfigure(configure);
        return services;
    }
}
