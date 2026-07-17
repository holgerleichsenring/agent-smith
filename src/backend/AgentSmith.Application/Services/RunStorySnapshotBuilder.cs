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
        var items = ledger.Entries
            .Select(e => new ProgressLedgerItemView(e.Id, e.Activity, StatusOf(e.Status), e.Target))
            .ToList();
        return RunStoryJson.Serialize(items);
    }

    /// <summary>
    /// Pairs the ratified criteria with the master's ordered dispositions the
    /// same way <see cref="RunOutcomeKeystone"/> does. A criterion the master
    /// reported nothing for is "unproven" — visible, never silently dropped.
    /// No ratified contract → null.
    /// </summary>
    public static string? BuildAcceptanceJson(
        RatifiedExpectation? expectation, MasterVerification? verification)
    {
        if (expectation is null) return null;

        var dispositions = verification?.AcceptanceDispositions;
        var criteria = expectation.Draft.Expected
            .Select((text, i) => CriterionOf(text, i < dispositions?.Count ? dispositions![i] : null))
            .ToList();

        return RunStoryJson.Serialize(
            new AcceptanceView(criteria, expectation.Outcome, expectation.RatifiedBy));
    }

    private static AcceptanceCriterionView CriterionOf(string text, AcceptanceDisposition? disposition)
    {
        if (disposition is null)
            return new AcceptanceCriterionView(text, AcceptanceCriterionStatuses.Unproven, null);

        var status = disposition.Status switch
        {
            AcceptanceStatus.Met => AcceptanceCriterionStatuses.Met,
            AcceptanceStatus.NotApplicable => AcceptanceCriterionStatuses.NotApplicable,
            _ => AcceptanceCriterionStatuses.Unmet,
        };
        var reason = string.IsNullOrWhiteSpace(disposition.Evidence) ? null : disposition.Evidence;
        return new AcceptanceCriterionView(text, status, reason);
    }

    private static string StatusOf(ProgressStatus status) => status switch
    {
        ProgressStatus.InProgress => "in_progress",
        ProgressStatus.Done => "done",
        _ => "pending",
    };
}
