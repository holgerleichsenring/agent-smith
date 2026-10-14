using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets.Checks;

/// <summary>
/// 2026-10-13-9a59h: whether a failed ticket is unchanged and untouched since its last attempt. True only on
/// what was observed: a check of that very attempt under a day old with the fingerprint unchanged and no
/// tracker act, and no pull-request entry since the cutoff that is neither ours nor a bot's. Null — never
/// "unchanged" — when nothing observed it: no check, an older one, or a last attempt that did not fail.
/// </summary>
public sealed class TicketStuckRule(TimeProvider clock)
{
    public static readonly TimeSpan FreshFor = TimeSpan.FromHours(24);

    public bool? Unchanged(Run last, TicketBetweenAttemptsState? check, IEnumerable<PrReviewRecordView> records) =>
        Fresh(last, check) is not { } fresh ? null
            : fresh.FingerprintUnchanged && fresh.Acts.Count == 0 && !PersonEntries(last, records).Any();

    /// <summary>What was observed after the failed last attempt, oldest first; empty when nothing was.</summary>
    public IReadOnlyList<TicketActedSinceView> ActedSince(
        Run last, TicketBetweenAttemptsState? check, IEnumerable<PrReviewRecordView> records)
    {
        if (last.Status != RunStatuses.Failed) return [];
        var acted = new List<TicketActedSinceView>();
        if (check is not null && check.RunId == last.Id)
        {
            acted.AddRange(check.Acts.Select(a => new TicketActedSinceView(a.Kind, a.Who, a.At)));
            if (!check.FingerprintUnchanged) acted.Add(new TicketActedSinceView("edit", null, check.CheckedAt));
        }
        acted.AddRange(PersonEntries(last, records).Select(e => new TicketActedSinceView("pr-review", e.Entry.AuthorLogin, e.Entry.At, e.Url)));
        return [.. acted.OrderBy(a => a.At)];
    }

    private TicketBetweenAttemptsState? Fresh(Run last, TicketBetweenAttemptsState? check) =>
        last.Status == RunStatuses.Failed && check is not null && check.RunId == last.Id
        && clock.GetUtcNow() - check.CheckedAt < FreshFor ? check : null;

    private static IEnumerable<(string Url, PrReviewEntryView Entry)> PersonEntries(Run last, IEnumerable<PrReviewRecordView> records)
    {
        var cutoff = new PreviousAttempt(last.Id, last.Status, last.StartedAt, true) { ActsReadAt = last.ActsReadAt };
        return records.SelectMany(r => r.Entries.Select(e => (r.Url, Entry: e)))
            .Where(e => !e.Entry.IsOurs && !e.Entry.IsBot && cutoff.Precedes(e.Entry.At));
    }
}
