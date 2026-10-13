using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-13-9a59l: an approval given in a worked ticket's own conversation, applied to the series the
/// branch holds NOW. The server has no clone and a live run may hold the branch, so the approval only
/// recorded what each spec should become; this run makes it the next revision, caused by the approval.
/// <para>
/// It answers only for a record that continues a worked series (a basis run) with an approval newer
/// than the branch set's. A never-worked hand-off is never merged: its empty dispositions over a stale
/// branch would stamp the new approval onto the old set. The approval answers a hand-back, so the
/// merged set carries none and is gated and built.
/// </para>
/// </summary>
public sealed class ApprovedContinuation(
    ContinuationMerge merge, ILogger<ApprovedContinuation> logger)
{
    /// <summary>The decision over the merged set, or null when the record continues nothing here.</summary>
    public SpecSourceResolver.Decision? Decide(SpecSet branch, SpecApprovalRecord? record, string cause)
    {
        ArgumentNullException.ThrowIfNull(branch);
        if (record?.Continuation is not { BasisRunId: not null } continuation
            || record.Approval is not { } approval || !approval.IsNewerThan(branch.Approval)) return null;

        var (specs, notes) = merge.Apply(branch, record.Set, continuation);
        var unstarted = specs.Count(s => !branch.Executed.Contains(s.SpecId, StringComparer.Ordinal));
        if (branch.Executed.Count + unstarted > SpecSet.MaxSpecs)
        {
            var why = $"the approval given in design conversation {approval.Conversation} was not merged: the "
                + $"series would hold {branch.Executed.Count} executed and {unstarted} unstarted spec(s), over "
                + $"the cap of {SpecSet.MaxSpecs}; approve a smaller series in the ticket's conversation";
            logger.LogWarning("Spec set {Key}: {Why}", branch.Key, why);
            return new SpecSourceResolver.Decision(SpecSource.BranchArtifact, branch, false, Cause: cause, Discarded: why);
        }

        logger.LogInformation(
            "Spec set {Key}: the approval given in conversation {Conversation} continues it ({Specs} spec(s), {Notes} note(s))",
            branch.Key, approval.Conversation, specs.Count, notes.Count);
        return new SpecSourceResolver.Decision(
            SpecSource.BranchArtifact, branch with { Specs = specs, Handback = null }, NeedsModel: false,
            Cause: ApprovedSetHandoff.CauseOf(record), Approval: approval,
            Discarded: notes.Count == 0 ? null : string.Join("; ", notes));
    }
}
