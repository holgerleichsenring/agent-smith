using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0344b: builds the run-story JSON payloads persisted on the run row at run
/// end — the p0341 progress ledger and the p0340 acceptance dispositions paired
/// with their ratified criteria. Pure mapping onto the camelCase wire records
/// (<see cref="ProgressLedgerItemView"/> / <see cref="AcceptanceView"/>), so the
/// stored JSON IS what the run-detail endpoint serves. Null in = null out —
/// a run without a ledger or without a ratified contract stores nothing and the
/// dashboard renders an honest empty state.
/// </summary>
public static class RunStorySnapshotBuilder
{
    public static string? BuildLedgerJson(ProgressLedger? ledger)
    {
        if (ledger is null || ledger.IsEmpty) return null;
        // p0356: Note rides along — mid-run flushes make the stored ledger a
        // resume seed, and the note is the working state a resumed run needs.
        var items = ledger.Entries
            .Select(e => new ProgressLedgerItemView(e.Id, e.Activity, StatusOf(e.Status), e.Target, e.Note))
            .ToList();
        return RunStoryJson.Serialize(items);
    }

    /// <summary>
    /// p0374a: the transitions of ONE accepted update_progress call, as the wire
    /// JSON the trail carries. Nothing changed → null, and no event is published:
    /// re-sending an unchanged checklist is not history.
    /// </summary>
    public static string? BuildTransitionsJson(IReadOnlyList<LedgerTransition>? transitions)
    {
        if (transitions is null || transitions.Count == 0) return null;
        var items = transitions
            .Select(t => new LedgerTransitionView(
                t.EntryId, t.Activity, StatusOrNull(t.From), StatusOrNull(t.To), CauseOf(t.Cause), t.Pass))
            .ToList();
        return RunStoryJson.Serialize(items);
    }

    private static string? StatusOrNull(ProgressStatus? status) =>
        status is null ? null : StatusOf(status.Value);

    private static string CauseOf(LedgerTransitionCause cause) => cause switch
    {
        LedgerTransitionCause.Added => LedgerTransitionCauses.Added,
        LedgerTransitionCause.ExplicitReopen => LedgerTransitionCauses.ExplicitReopen,
        LedgerTransitionCause.RegressionRefused => LedgerTransitionCauses.RegressionRefused,
        LedgerTransitionCause.OmissionRefused => LedgerTransitionCauses.OmissionRefused,
        LedgerTransitionCause.Dropped => LedgerTransitionCauses.Dropped,
        _ => LedgerTransitionCauses.ModelUpdate,
    };

    /// <summary>
    /// 2026-08-25-7f5a: the acceptance half moved to <see cref="AcceptanceSnapshot"/> when it
    /// gained a second source. Kept as the one entry point the handler calls.
    /// </summary>
    public static string? BuildAcceptanceJson(
        RatifiedExpectation? expectation, MasterVerification? verification,
        RunAccounts? accounts = null) =>
        AcceptanceSnapshot.Build(expectation, verification, accounts);

    private static string StatusOf(ProgressStatus status) => status switch
    {
        ProgressStatus.InProgress => "in_progress",
        ProgressStatus.Done => "done",
        _ => "pending",
    };
}
