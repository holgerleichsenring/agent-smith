using AgentSmith.Contracts.Series;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-11-25-96aea: the series wakes a test reads back — every ticket is held by a series, and what woke it is kept.</summary>
internal sealed class RecordingSeriesWakes : ISeriesWakes
{
    public List<(string Project, string TicketId, SeriesWake Wake)> Woken { get; } = [];

    public Task<bool> WakeAsync(string project, string ticketId, SeriesWake wake, CancellationToken cancellationToken)
    {
        lock (Woken) Woken.Add((project, ticketId, wake));
        return Task.FromResult(true);
    }
}
