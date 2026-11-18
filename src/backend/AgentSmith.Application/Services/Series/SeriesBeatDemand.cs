using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: why a series needs a beat now, from its record and its tickets' facts; null when it
/// needs none.</summary>
public sealed class SeriesBeatDemand
{
    public Task<string?> WhyAsync(
        SeriesRunRecord record, IReadOnlyList<SeriesTicketFact> facts, DateTimeOffset now, CancellationToken ct)
    {
        _ = ct;
        return Task.FromResult(SeriesBeatDue.Why(record, facts, now));
    }
}
