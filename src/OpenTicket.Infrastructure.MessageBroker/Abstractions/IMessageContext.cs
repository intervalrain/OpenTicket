namespace OpenTicket.Infrastructure.MessageBroker.Abstractions;

/// <summary>
/// Provides context for processing a received message.
/// Abstracts provider-specific acknowledgment mechanisms.
/// </summary>
public interface IMessageContext<out TMessage> where TMessage : IMessage
{
    /// <summary>
    /// The received message.
    /// </summary>
    TMessage Message { get; }

    /// <summary>
    /// The topic/queue name the message was received from.
    /// </summary>
    string Topic { get; }

    /// <summary>
    /// The partition number the message was received from.
    /// </summary>
    int Partition { get; }

    /// <summary>
    /// Acknowledges successful processing of the message.
    /// </summary>
    Task AckAsync(CancellationToken ct = default);

    /// <summary>
    /// Rejects the message, indicating processing failed.
    /// </summary>
    /// <param name="requeue">Whether to requeue the message for retry.</param>
    Task NakAsync(bool requeue = true, CancellationToken ct = default);

    /// <summary>
    /// Delays reprocessing of the message by the specified duration.
    /// Not all providers support this - falls back to Nak with requeue if unsupported.
    /// </summary>
    Task DelayAsync(TimeSpan delay, CancellationToken ct = default);
}

/// <summary>
/// Default implementation of IMessageContext for providers that support it.
/// </summary>
public sealed class MessageContext<TMessage> : IMessageContext<TMessage> where TMessage : IMessage
{
    private readonly Func<CancellationToken, Task> _ack;
    private readonly Func<bool, CancellationToken, Task> _nak;
    private readonly Func<TimeSpan, CancellationToken, Task>? _delay;

    public MessageContext(
        TMessage message,
        string topic,
        int partition,
        Func<CancellationToken, Task> ack,
        Func<bool, CancellationToken, Task> nak,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        Message = message;
        Topic = topic;
        Partition = partition;
        _ack = ack;
        _nak = nak;
        _delay = delay;
    }

    public TMessage Message { get; }
    public string Topic { get; }
    public int Partition { get; }

    public Task AckAsync(CancellationToken ct = default) => _ack(ct);
    public Task NakAsync(bool requeue = true, CancellationToken ct = default) => _nak(requeue, ct);
    public Task DelayAsync(TimeSpan delay, CancellationToken ct = default) =>
        _delay?.Invoke(delay, ct) ?? NakAsync(true, ct);
}
