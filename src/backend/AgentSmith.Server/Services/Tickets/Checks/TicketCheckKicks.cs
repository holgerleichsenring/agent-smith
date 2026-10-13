using System.Threading.Channels;
using Microsoft.Extensions.Caching.Memory;

namespace AgentSmith.Server.Services.Tickets.Checks;

/// <summary>
/// 2026-10-13-9a59h: the list read's door to the between-attempts check. Without it the Stuck tile reads
/// nothing until someone opens each detail. The list offers its page's failed tickets and never waits:
/// at most ten per read, each ticket at most once per five minutes on this replica, queued to
/// <see cref="TicketCheckKickWorker"/>. The check's own five-minute reuse bounds it across replicas.
/// </summary>
public sealed class TicketCheckKicks(IMemoryCache cache)
{
    public const int PerRead = 10;
    public static readonly TimeSpan PerTicket = TimeSpan.FromMinutes(5);

    private readonly Channel<TicketCheckKick> _queue = Channel.CreateBounded<TicketCheckKick>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    internal ChannelReader<TicketCheckKick> Reader => _queue.Reader;

    /// <summary>Queues up to <see cref="PerRead"/> of the tickets not kicked in the last five minutes.</summary>
    public int Offer(IEnumerable<TicketCheckKick> tickets)
    {
        var kicked = 0;
        foreach (var ticket in tickets)
        {
            if (kicked == PerRead) break;
            var key = $"ticket-check-kick:{ticket.Project}/{ticket.TicketId}";
            if (cache.TryGetValue(key, out _)) continue;
            cache.Set(key, true, PerTicket);
            if (_queue.Writer.TryWrite(ticket)) kicked++;
        }
        return kicked;
    }
}

/// <summary>2026-10-13-9a59h: one ticket offered to the between-attempts check.</summary>
public sealed record TicketCheckKick(string Project, string TicketId);
