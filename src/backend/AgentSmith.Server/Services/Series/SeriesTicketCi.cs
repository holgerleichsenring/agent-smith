using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-17-0207d: a member ticket's CI, read from the pull-request records the CI loop keeps — the series
/// watches nothing itself. Red wins, then pending, then green; a red one carries its first failed check.</summary>
public sealed class SeriesTicketCi(IPrReviewRecordReader records)
{
    public async Task<SeriesTicketFact> WithAsync(SeriesTicketFact fact, string project, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var ci = (await records.ByTicketAsync(project, fact.Ticket.TicketId, ct))
            .Where(r => r.Ci is { } c && c.IsOn(r.HeadSha)).Select(r => r.Ci!).ToList();
        if (ci.Count == 0) return fact;
        var red = ci.FirstOrDefault(c => c.State == CiStates.Red);
        var state = red is not null ? CiStates.Red : ci.Any(c => c.State == CiStates.Pending) ? CiStates.Pending
            : ci.All(c => c.State == CiStates.Green) ? CiStates.Green : null;
        var failure = red?.Failures.FirstOrDefault() is { } f ? $"{f.Name}: {f.Excerpt.Split('\n', 2)[0].Trim()}" : null;
        return fact with { CiState = state, CiFailure = failure };
    }
}
