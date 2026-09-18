using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79a: merges a re-approved set over the set the branch already carries — or it
/// deletes history and re-runs finished phases.
/// <para>
/// THE MERGE IS POSITIONAL, and the rule is written out because the two obvious readings
/// disagree. A carried record has <c>Executed = []</c>, so its own UnexecutedTail is the WHOLE
/// set and reading the tail off the record would re-run everything. The parser's shape is
/// positional and so are the ids, minted from the phase's index. So the merged set is the
/// BRANCH's ExecutedHead followed by the record's phases from index <c>ExecutedHead.Count</c>
/// onward.
/// </para>
/// <para>
/// A record with FEWER phases than that head is REFUSED, naming the phases it would drop:
/// silently shortening a sequence whose work is already in the branch history is the one thing
/// a merge must not do. An edit the record makes at or before the head is discarded, and the
/// merge says which — an executed phase is append-only.
/// </para>
/// </summary>
public static class ApprovedSetMerge
{
    /// <summary>The merged set, or the reason it was refused; <paramref name="Note"/> names an
    /// edit the merge discarded, so the run can report it.</summary>
    public sealed record Result(SpecSet? Set, string? Error, string? Note = null);

    public static Result Over(SpecSet approved, SpecSet? branch)
    {
        ArgumentNullException.ThrowIfNull(approved);
        if (branch is null) return new Result(approved with { Source = SpecSource.Approved }, null);

        var head = branch.ExecutedHead;
        // Measured against BOTH: the head is the contiguous prefix, but Executed is copied
        // wholesale, so a hand-edited set.yaml listing a non-contiguous executed phase would
        // otherwise let a shorter re-approval through.
        if (approved.Phases.Count < head.Count || approved.Phases.Count < branch.Executed.Count)
            return new Result(null, Dropped(approved, head, branch.Executed.Count));

        var edited = EditedHead(approved, head);
        // A re-approval that adds nothing past the head is a SILENT no-op: the sequence would
        // report "nothing left to run" and the run would succeed having discarded every edit.
        if (approved.Phases.Count == head.Count && edited is not null)
            return new Result(null, NothingLeftToRun(edited));

        return new Result(
            approved with
            {
                Key = branch.Key,
                Phases = [.. head, .. approved.Phases.Skip(head.Count)],
                // The reader rebuilds each phase's carried segments from the index; writing an
                // empty accounting over it would erase what a derived predecessor recorded.
                Accounting = branch.Accounting,
                TicketPinnedWhole = branch.TicketPinnedWhole,
                Revisions = branch.Revisions,
                // Carried, as Finalize already carries it on a revision written without a model.
                TicketFingerprint = branch.TicketFingerprint,
                Executed = branch.Executed,
                // A hand-back says derivation could not specify the ticket, and an approval is
                // the answer to that question.
                Handback = null,
                Source = SpecSource.Approved,
            },
            null,
            edited);
    }

    private static string Dropped(
        SpecSet approved, IReadOnlyList<SpecPhase> head, int executedCount) =>
        "the re-approved set has " + approved.Phases.Count + " phase(s) but "
        + Math.Max(head.Count, executedCount) + " have already run on the ticket branch; it would drop "
        + string.Join(", ", head.Skip(approved.Phases.Count).Select(p => p.PhaseId))
        + " — an executed phase is append-only, so a correction to one is a new phase";

    private static string NothingLeftToRun(string edited) =>
        "the re-approved set adds no phase past the ones that already ran, and " + edited
        + ". Publishing it would leave the run reporting success with nothing to do — a "
        + "correction to executed work is a NEW phase, appended after them";

    private static string? EditedHead(SpecSet approved, IReadOnlyList<SpecPhase> head)
    {
        var edited = head
            .Where((phase, i) => !Same(phase, approved.Phases[i]))
            .Select(p => p.PhaseId)
            .ToList();
        return edited.Count == 0
            ? null
            : $"the re-approved set edits {string.Join(", ", edited)}, which already ran — "
              + "the executed phases are kept exactly as they ran and the edit is discarded";
    }

    private static bool Same(SpecPhase executed, SpecPhase proposed) =>
        string.Equals(executed.PhaseId, proposed.PhaseId, StringComparison.Ordinal)
        && string.Equals(executed.Draft.Yaml, proposed.Draft.Yaml, StringComparison.Ordinal);
}
