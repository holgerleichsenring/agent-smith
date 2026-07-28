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
/// p0384: targets are repo-qualified by the PLAN — the multi-repo plan rules
/// require every step's target file to carry its repository prefix (the same
/// prefix the filesystem tools route on), so the seed passes the step target
/// through verbatim and ledger entries resolve against the right repo's diff.
/// The keystone's run-level cross-check (p0373) never parses targets, so no
/// second format exists to keep in sync.
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
