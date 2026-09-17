using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79a: the APPROVED arm of the source precedence — a fourth source below the
/// branch artifact and above the ticket description.
/// <para>
/// The record is a VERSIONED source, not a one-shot hand-off. An approved set can be approved
/// again, so "carried once, then the branch forever" would make the second approval
/// unreachable. The branch artifact keeps winning UNLESS the record's approval instant is newer
/// than the one <c>set.yaml</c> was published from — everything else about the branch keeps
/// winning too, which is what an executed phase needs.
/// </para>
/// </summary>
public sealed class ApprovedSetSource(ILogger<ApprovedSetSource> logger)
{
    /// <summary>The decision the approval makes, or null when it does not make one.</summary>
    public SpecSourceResolver.Decision? Decide(
        SpecApprovalRecord? record, SpecSetReadResult? branchArtifact, string key)
    {
        if (record is null) return null;
        // The precedence FIRST. A record that does not win cannot refuse the run: an old
        // over-cap record left in the store would otherwise brick every later run of a ticket
        // whose branch already carries a perfectly good set.
        if (branchArtifact is not null && !IsNewerThanTheBranch(record, branchArtifact.Set))
        {
            logger.LogInformation(
                "The approved set for {Key} is not newer than the approval the ticket branch was "
                + "published from — the branch artifact stands", key);
            return null;
        }

        if (record.Set.Phases.Count > SpecSet.MaxPhases)
            return Refused(
                $"the approved set for {key} has {record.Set.Phases.Count} phases and the cap is "
                + $"{SpecSet.MaxPhases}; it is a programme and is split in the design conversation, "
                + "never truncated by the run");

        var merged = ApprovedSetMerge.Over(record.Set, branchArtifact?.Set);
        if (merged.Error is not null) return Refused(merged.Error);
        if (merged.Note is not null) logger.LogWarning("{Note}", merged.Note);
        logger.LogInformation(
            "Spec set {Key} comes from the approval given in conversation {Conversation} "
            + "({Phases} phase(s)); no derivation", key, Conversation(record), merged.Set!.Phases.Count);
        return new SpecSourceResolver.Decision(
            SpecSource.Approved, merged.Set, NeedsModel: false, Cause: CauseOf(record, merged.Note));
    }

    /// <summary>The revision cause an approved set writes — it names the approval it came from,
    /// and the edit it discarded when there was one.</summary>
    public static string CauseOf(SpecApprovalRecord record, string? note)
    {
        ArgumentNullException.ThrowIfNull(record);
        var cause = $"{SpecRevisionCause.Approval} {Conversation(record)}";
        return note is null ? cause : $"{cause} — {note}";
    }

    private static bool IsNewerThanTheBranch(SpecApprovalRecord record, SpecSet branch) =>
        record.Approval is { } approval && approval.IsNewerThan(branch.Approval);

    private static string Conversation(SpecApprovalRecord record) =>
        string.IsNullOrWhiteSpace(record.Approval?.Conversation)
            ? "(unnamed)" : record.Approval!.Conversation;

    private SpecSourceResolver.Decision Refused(string error)
    {
        logger.LogWarning("The approved spec set was refused: {Error}", error);
        return new SpecSourceResolver.Decision(SpecSource.Approved, null, false, error);
    }
}
