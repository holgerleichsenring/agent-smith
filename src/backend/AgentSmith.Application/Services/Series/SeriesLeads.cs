using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-27-3532b: the one rule for whether a series leads its tickets' run questions — it is active, no series of one
/// (2026-11-27-ecf2e, operator: unless it talks in its ticket's conversation — one question, one place),
/// and its master can act: its loop has not failed since it last judged and its budget is not spent. A series that does
/// not lead them leaves the question to the ticket page, so a master that cannot act never hides it. The hold facts, the
/// beat, the answer endpoint and the resume sweep all read this.
/// </summary>
public static class SeriesLeads
{
    /// <param name="configuredBudgetUsd">dialogue.series_master_budget_usd; the record adds what was raised on it.</param>
    public static bool Questions(SeriesRunRecord? record, decimal configuredBudgetUsd) =>
        record is { IsActive: true, MasterFailedAt: null } && (!record.IsOne || record.Conversation.Length > 0) // ecf2e
        && record.MasterCostUsd < record.BudgetUsd(configuredBudgetUsd);

    /// <summary>2026-11-27-e451: whether the series leads its tickets' merges — its review makes them the master's
    /// (<see cref="SeriesBeatDue.MasterMerges"/>), and it leads their pull requests (<see cref="PullRequests"/>). One that
    /// cannot act leaves the merge to a person, so a stranded pull request never reads as merging.</summary>
    public static bool Merges(SeriesRunRecord? record, decimal configuredBudgetUsd) =>
        PullRequests(record, configuredBudgetUsd) && SeriesBeatDue.MasterMerges(record!);

    /// <summary>2026-11-27-e451: whether the series' master leads its tickets' pull requests — a conflict (refresh_branch) and a
    /// draft (merge_pull_request publishes it) are its steps: the series is active, a series of one included, and its master can
    /// act (its loop has not failed since it last judged, its budget is not spent).</summary>
    public static bool PullRequests(SeriesRunRecord? record, decimal configuredBudgetUsd) =>
        record is { IsActive: true, MasterFailedAt: null } && record.MasterCostUsd < record.BudgetUsd(configuredBudgetUsd);
}
