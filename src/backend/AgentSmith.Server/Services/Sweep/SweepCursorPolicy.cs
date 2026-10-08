using AgentSmith.Contracts.Sweep;

namespace AgentSmith.Server.Services.Sweep;

/// <summary>
/// 2026-10-08-9e6e: where a cursor moves after a read. An uncut read moves to the later of its last
/// item and the read's start less five seconds, so an idle source re-reads nothing next time. A cut
/// read with a resume point keeps its time and resumes there; one without (an ascending read) moves
/// to its last item.
/// </summary>
public static class SweepCursorPolicy
{
    public static readonly TimeSpan Margin = TimeSpan.FromSeconds(5);

    public static SweepPosition After(SweepPosition from, ChangedPage page, DateTimeOffset readStart)
    {
        var last = page.Items.Count > 0 ? page.Items.Max(i => i.At) : DateTimeOffset.MinValue;
        if (!page.Cut) return new SweepPosition(Max(from.At, Max(last, readStart - Margin)));
        return page.Resume is not null ? new SweepPosition(from.At, page.Resume) : new SweepPosition(Max(from.At, last));
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
