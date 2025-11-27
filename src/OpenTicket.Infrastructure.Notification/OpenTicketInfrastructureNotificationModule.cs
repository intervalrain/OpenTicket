using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Infrastructure.Notification.Abstractions;
using OpenTicket.Infrastructure.Notification.Internal;
using OpenTicket.Infrastructure.Notification.Smtp;

namespace OpenTicket.Infrastructure.Notification;

/// <summary>
/// Module for registering notification services.
/// </summary>
public static class OpenTicketInfrastructureNotificationModule
{
    /// <summary>
    /// Adds notification services with the specified channels.
    /// The mode (Console/Production) is determined by configuration.
    /// </summary>
    /// <example>
    /// // Enable Email and SMS channels
    /// services.AddNotification(configuration, NotificationChannel.Email | NotificationChannel.Sms);
    ///
    /// // appsettings.json controls the mode:
    /// // "Notification": { "Mode": "Console" }  -> logs instead of sending
    /// // "Notification": { "Mode": "Production" } -> sends via real providers
    /// </example>
    public static IServiceCollection AddNotification(
        this IServiceCollection services,
        IConfiguration configuration,
        NotificationChannel channels)
    {
        if (channels == NotificationChannel.None)
            throw new ArgumentException("At least one notification channel must be specified", nameof(channels));

        // Register options
        services.Configure<NotificationOptions>(
            configuration.GetSection(NotificationOptions.SectionName));
        services.Configure<SmtpOptions>(
            configuration.GetSection(SmtpOptions.SectionName));

        // Register notification senders based on channels and mode
        if (channels.HasFlag(NotificationChannel.Email)) RegisterEmailSender(services);
        if (channels.HasFlag(NotificationChannel.Sms)) RegisterSmsSender(services);
        if (channels.HasFlag(NotificationChannel.Line)) RegisterLineSender(services);
        if (channels.HasFlag(NotificationChannel.Push)) RegisterPushSender(services);

        // Register notification service
        services.AddSingleton<INotificationService, NotificationService>();

        // Auto-register all IIntegrationEventHandler<T> implementations from this assembly
        services.AddIntegrationEventHandlersFromAssemblyContaining<NotificationService>();

        return services;
    }

    /// <summary>
    /// Adds notification services with default Email channel.
    /// </summary>
    public static IServiceCollection AddNotification(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddNotification(configuration, NotificationChannel.Email);
    }

    #region Sender Registration

    private static void RegisterEmailSender(IServiceCollection services)
    {
        services.AddSingleton<INotificationSender>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<NotificationOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<ConsoleNotificationSender>>();

            return options.Mode switch
            {
                NotificationMode.Production => ActivatorUtilities.CreateInstance<SmtpNotificationSender>(sp),
                _ => new ConsoleNotificationSender(NotificationChannel.Email, logger)
            };
        });
    }

    private static void RegisterSmsSender(IServiceCollection services)
    {
        services.AddSingleton<INotificationSender>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<NotificationOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<ConsoleNotificationSender>>();

            return options.Mode switch
            {
                NotificationMode.Production => throw new NotImplementedException("SMS sender is not yet implemented"),
                _ => new ConsoleNotificationSender(NotificationChannel.Sms, logger)
            };
        });
    }

    private static void RegisterLineSender(IServiceCollection services)
    {
        services.AddSingleton<INotificationSender>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<NotificationOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<ConsoleNotificationSender>>();

            return options.Mode switch
            {
                NotificationMode.Production => throw new NotImplementedException("LINE sender is not yet implemented"),
                _ => new ConsoleNotificationSender(NotificationChannel.Line, logger)
            };
        });
    }

    private static void RegisterPushSender(IServiceCollection services)
    {
        services.AddSingleton<INotificationSender>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<NotificationOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<ConsoleNotificationSender>>();

            return options.Mode switch
            {
                NotificationMode.Production => throw new NotImplementedException("Push sender is not yet implemented"),
                _ => new ConsoleNotificationSender(NotificationChannel.Push, logger)
            };
        });
    }

    #endregion
}
