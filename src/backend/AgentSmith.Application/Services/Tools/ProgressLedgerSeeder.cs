using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Progress;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0341, p0394a: seeds the progress ledger 1:1 from the ratified phase spec's
/// steps — the model opens on the checklist instead of re-deriving it, and the
/// seed and the keystone's verification finally share one source. Ids are
/// FRAMEWORK-ASSIGNED (the spec step's own id) so the model does not invent
/// them, making full-state replacement a reconcile-by-id; each seeded item
/// carries the spec step's target hint for the done-status honesty diagnostic.
/// p0384: targets are repo-qualified by the SPEC — the seed passes the step's
/// target through verbatim so ledger entries resolve against the right repo's
/// diff. No spec (scan/dialog surfaces) yields an empty seed — the master
/// fills it live.
/// </summary>
public static class ProgressLedgerSeeder
{
    public static IReadOnlyList<ProgressLedgerEntry> Seed(PhaseDraft? draft)
    {
        if (draft is null || draft.Steps.Count == 0)
            return Array.Empty<ProgressLedgerEntry>();
        return draft.Steps
            .Select(s => new ProgressLedgerEntry(
                Id: s.Id,
                Activity: s.Action,
                Status: ProgressStatus.Pending,
                Target: s.Target))
            .ToList();
    }
}
