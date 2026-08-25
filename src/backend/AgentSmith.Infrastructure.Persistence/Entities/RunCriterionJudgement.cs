namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-08-25-e257: one human judgement of one criterion's disposition.
/// <para>
/// A run that fails on a wrong criterion is indistinguishable from one that fails on a right
/// one — same state, same colour, same cost. Fourteen phases have tuned the delivery account,
/// each on a single failed run, because the only failures that announce themselves are
/// mechanical. The operator already knows which verdicts were wrong; this is where they say
/// so.
/// </para>
/// <para>
/// Its own row, NOT a field inside the acceptance snapshot: the story event's applier assigns
/// that payload wholesale on every publish, so a resume, a retry or a repair pass would
/// silently destroy the labels stored in it. A judgement about a snapshot has to outlive the
/// snapshot.
/// </para>
/// <para>
/// Keyed by a digest of the criterion TEXT rather than its position, because the criteria of
/// a re-derived phase can reorder and a label that moved to a different criterion is worse
/// than no label at all.
/// </para>
/// </summary>
public sealed class RunCriterionJudgement : EntityBase
{
    public long Id { get; set; }

    public string RunId { get; set; } = string.Empty;

    /// <summary>A digest of the normalised criterion text — what the unique index is on.</summary>
    public string CriterionKey { get; set; } = string.Empty;

    /// <summary>The criterion as it was judged, so a reader needs no second lookup.</summary>
    public string CriterionText { get; set; } = string.Empty;

    /// <summary>What the account said: the vocabulary of AcceptanceCriterionStatuses.</summary>
    public string MachineStatus { get; set; } = string.Empty;

    /// <summary>What was actually true, in the same vocabulary.</summary>
    public string HumanStatus { get; set; } = string.Empty;

    /// <summary>Required. A label nobody can audit later is worse than no label.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Who judged. A corpus of judgements with no author cannot be weighted,
    /// questioned or withdrawn.</summary>
    public string Author { get; set; } = string.Empty;

    public DateTimeOffset RecordedAt { get; set; }
}
