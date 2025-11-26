namespace OpenTicket.Application.Tickets.Settings;

public sealed class TicketSettings
{
    public const string SectionName = "Ticket";

    public const int DefaultLockTtlSeconds = 120;

    public int LockTtlSeconds { get; set; } = DefaultLockTtlSeconds;

    public TimeSpan LockTtl => TimeSpan.FromSeconds(LockTtlSeconds);
}
