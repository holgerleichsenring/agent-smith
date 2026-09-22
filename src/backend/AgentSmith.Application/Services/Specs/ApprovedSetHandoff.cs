using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-22-6ad7: the ONE thing the approval record still supplies as a SET — the hand-off for
/// a ticket whose branch carries nothing at the spec path.
/// <para>
/// It replaces the versioned arm 2026-09-17-0e79a put above the branch. That arm compared two
/// approval instants across two carriers before the branch was consulted, and merged the winner
/// over it; the branch is now the only set a run reads, so there is nothing left to compare and
/// nothing left to merge. What remains is a hand-off that fires exactly once, on a ticket filed
/// before the branch write existed or one whose write failed for an ordinary reason: the record's
/// set becomes the run's, the run publishes it to the branch, and every later run reads it there.
/// </para>
/// <para>
/// It answers only for NOTHING AT THE PATH. A branch that holds something this run could not read
/// is an operator's edit gone wrong, and replacing it from a copy is how the edit disappears.
/// </para>
/// </summary>
public sealed class ApprovedSetHandoff(ILogger<ApprovedSetHandoff> logger)
{
    /// <summary>The set the record hands over, or null when there is no record to hand one.</summary>
    public SpecSourceResolver.Decision? Decide(SpecApprovalRecord? record, string key)
    {
        if (record is null) return null;
        logger.LogInformation(
            "Spec set {Key} comes from the approval given in conversation {Conversation} "
            + "({Phases} phase(s)); the ticket branch carries none yet",
            key, Conversation(record), record.Set.Phases.Count);
        return new SpecSourceResolver.Decision(
            SpecSource.Approved,
            record.Set with { Source = SpecSource.Approved },
            NeedsModel: false,
            Cause: CauseOf(record));
    }

    /// <summary>The revision cause an approved set writes — it names the approval it came from.
    /// Filing's own branch write spells it the same way, so the first publish is a no-op.</summary>
    public static string CauseOf(SpecApprovalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return $"{SpecRevisionCause.Approval} {Conversation(record)}";
    }

    private static string Conversation(SpecApprovalRecord record) =>
        string.IsNullOrWhiteSpace(record.Approval?.Conversation)
            ? "(unnamed)" : record.Approval!.Conversation;
}
