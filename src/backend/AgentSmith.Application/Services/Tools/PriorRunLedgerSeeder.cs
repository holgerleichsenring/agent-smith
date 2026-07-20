using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0356: turns the latest same-ticket run's persisted ledger into a RESUME
/// seed — a reaped/crashed run's mid-run flushes (ProgressLedgerFlusher) become
/// the checklist the next run of the SAME ticket opens on. Gated: the prior run
/// must have PROGRESSED PAST BOOTSTRAP (at least one non-pending item — an
/// all-pending ledger is a bootstrap abort and would resurrect a checklist
/// nothing validated) and be young enough that its conventions still hold.
/// Distinct from cross-run context ingestion, which stays successful-runs-only;
/// a resume deliberately reads the latest run regardless of outcome.
/// </summary>
public static class PriorRunLedgerSeeder
{
    /// <summary>Age cap on the carry-over: a reap-resume happens within hours;
    /// past this window the repo has moved on and a fresh plan beats a stale
    /// checklist.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(72);

    public static IReadOnlyList<ProgressLedgerEntry> Seed(PriorRunLedger? prior, DateTimeOffset now)
    {
        if (prior is null || prior.Items.Count == 0) return Array.Empty<ProgressLedgerEntry>();
        if (now - prior.StartedAt > MaxAge) return Array.Empty<ProgressLedgerEntry>();
        if (!ProgressedPastBootstrap(prior.Items)) return Array.Empty<ProgressLedgerEntry>();
        return prior.Items
            .Take(ProgressLedger.MaxItems)
            .Select(ToEntry)
            .ToList();
    }

    private static bool ProgressedPastBootstrap(IReadOnlyList<ProgressLedgerItemView> items) =>
        items.Any(i => !string.Equals(i.Status, "pending", StringComparison.OrdinalIgnoreCase));

    // A resumed run re-verifies the interrupted step: in_progress comes back as
    // pending (the work was cut off mid-flight); done stays done, note and all.
    private static ProgressLedgerEntry ToEntry(ProgressLedgerItemView item) =>
        new(item.Id, item.Activity, MapStatus(item.Status), item.Target, item.Note);

    private static ProgressStatus MapStatus(string status) =>
        string.Equals(status, "done", StringComparison.OrdinalIgnoreCase)
            ? ProgressStatus.Done
            : ProgressStatus.Pending;
}
