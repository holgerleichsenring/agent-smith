using AgentSmith.Application.Services;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Delivery;

/// <summary>
/// 2026-10-25-0099b: the period a request names, rolling and UTC, ending at the next whole second so the newest
/// run's id falls inside: day is 24 one-hour slots, week 7 one-day slots, month 30 one-day slots; the previous
/// period is the same length before. Any other name is no period.
/// <para>2026-11-12-aeddb: an offset steps the window back that many whole periods, so offset 1 is the period the
/// current one is compared with; a negative offset, or one beyond <see cref="MaxOffset"/>, is no window.</para>
/// </summary>
public sealed class DeliveryWindowFactory(TimeProvider clock)
{
    public const string Day = "day";
    public const string Week = "week";
    public const string Month = "month";
    public const int MaxOffset = 1000;

    public DeliveryWindow? Create(string? period, int offset)
    {
        if (offset is < 0 or > MaxOffset) return null;
        var name = string.IsNullOrWhiteSpace(period) ? Week : period.Trim().ToLowerInvariant();
        (TimeSpan Slot, int Count)? shape = name switch
        {
            Day => (TimeSpan.FromHours(1), 24),
            Week => (TimeSpan.FromDays(1), 7),
            Month => (TimeSpan.FromDays(1), 30),
            _ => null,
        };
        return shape is { } s ? Window(name, s.Slot, s.Count, offset) : null;
    }

    private DeliveryWindow Window(string name, TimeSpan slot, int count, int offset)
    {
        var now = clock.GetUtcNow();
        var end = new DateTimeOffset(now.UtcTicks - now.UtcTicks % TimeSpan.TicksPerSecond, TimeSpan.Zero).AddSeconds(1)
            - slot * count * offset;
        var start = end - slot * count;
        return new DeliveryWindow(name, Span(start, end), Span(start - slot * count, start),
            [.. Enumerable.Range(0, count).Select(i => Span(start + slot * i, start + slot * (i + 1)))]);
    }

    private static DeliverySpan Span(DateTimeOffset from, DateTimeOffset to) =>
        new(from, to, RunIdGenerator.TimestampOf(from), RunIdGenerator.TimestampOf(to));
}
