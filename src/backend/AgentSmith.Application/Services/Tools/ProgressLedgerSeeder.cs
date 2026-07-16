using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0341: seeds the progress ledger 1:1 from the ratified plan's steps — the model
/// opens on the checklist instead of re-deriving it. Ids are FRAMEWORK-ASSIGNED
/// (the plan step order) so the model does not invent them, making full-state
/// replacement a reconcile-by-id; each seeded item carries the plan step's target
/// file for the done-status honesty diagnostic. No plan (fix-bug self-planning)
/// yields an empty seed — the master fills it live.
/// </summary>
public static class ProgressLedgerSeeder
{
    public static IReadOnlyList<ProgressLedgerEntry> Seed(Plan? plan)
    {
        if (plan is null || plan.Steps.Count == 0)
            return Array.Empty<ProgressLedgerEntry>();
        return plan.Steps
            .Select(s => new ProgressLedgerEntry(
                Id: s.Order.ToString(),
                Activity: s.Description,
                Status: ProgressStatus.Pending,
                Target: s.TargetFile?.ToString()))
            .ToList();
    }
}
