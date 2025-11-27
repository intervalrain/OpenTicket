using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using OpenTicket.Infrastructure.Notification.Abstractions;

namespace OpenTicket.Infrastructure.Notification.Smtp;

/// <summary>
/// SMTP-based email notification sender using MailKit.
/// </summary>
public sealed class SmtpNotificationSender : INotificationSender, IDisposable
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpNotificationSender> _logger;
    private SmtpClient? _client;
    private bool _disposed;

    public SmtpNotificationSender(
        IOptions<SmtpOptions> options,
        ILogger<SmtpNotificationSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.Email;

    public async Task<NotificationResult> SendAsync(NotificationMessage notification, CancellationToken ct = default)
    {
        try
        {
            var message = CreateMessage(notification);
            var client = await GetClientAsync(ct);

            var response = await client.SendAsync(message, ct);

            _logger.LogInformation(
                "Email sent successfully to {Recipient}. Subject: {Subject}, MessageId: {MessageId}",
                notification.Recipient,
                notification.Subject,
                message.MessageId);

            return NotificationResult.Ok(notification.Id, message.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send email to {Recipient}. Subject: {Subject}",
                notification.Recipient,
                notification.Subject);

            return NotificationResult.Fail(notification.Id, ex.Message);
        }
    }

    private MimeMessage CreateMessage(NotificationMessage notification)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(_options.SenderName, _options.SenderEmail));
        message.To.Add(MailboxAddress.Parse(notification.Recipient));
        message.Subject = notification.Subject;

        var builder = new BodyBuilder
        {
            TextBody = notification.Body,
            HtmlBody = FormatHtmlBody(notification.Body)
        };

        message.Body = builder.ToMessageBody();

        return message;
    }

    private static string FormatHtmlBody(string body)
    {
        // Simple HTML wrapper for the plain text body
        var escapedBody = System.Net.WebUtility.HtmlEncode(body)
            .Replace("\n", "<br/>");

        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <style>
                    body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }
                    .container { max-width: 600px; margin: 0 auto; padding: 20px; }
                </style>
            </head>
            <body>
                <div class="container">
                    {{escapedBody}}
                </div>
            </body>
            </html>
            """;
    }

    private async Task<SmtpClient> GetClientAsync(CancellationToken ct)
    {
        if (_client is { IsConnected: true })
            return _client;

        _client?.Dispose();
        _client = new SmtpClient();

        _client.Timeout = _options.TimeoutSeconds * 1000;

        // Use StartTls for port 587, SSL for port 465
        var secureSocketOptions = _options.Port == 587
            ? SecureSocketOptions.StartTls
            : (_options.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None);

        await _client.ConnectAsync(
            _options.Host,
            _options.Port,
            secureSocketOptions,
            ct);

        if (!string.IsNullOrEmpty(_options.Username))
        {
            await _client.AuthenticateAsync(_options.Username, _options.Password, ct);
        }

        return _client;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _client?.Disconnect(true);
        _client?.Dispose();
        _disposed = true;
    }
}
