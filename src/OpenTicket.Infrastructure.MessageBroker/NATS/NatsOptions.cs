namespace OpenTicket.Infrastructure.MessageBroker.Nats;

/// <summary>
/// NATS-specific configuration options.
/// </summary>
public class NatsOptions
{
    public const string SectionName = "MessageBroker:Nats";

    /// <summary>
    /// NATS server URL.
    /// </summary>
    public string Url { get; set; } = "nats://localhost:4222";

    /// <summary>
    /// JetStream domain (optional).
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// Stream name prefix.
    /// </summary>
    public string StreamPrefix { get; set; } = "MSGBROKER";

    /// <summary>
    /// Maximum age of messages in the stream.
    /// </summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Acknowledgment wait timeout.
    /// </summary>
    public TimeSpan AckWait { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of delivery attempts.
    /// </summary>
    public int MaxDeliver { get; set; } = 5;
}
