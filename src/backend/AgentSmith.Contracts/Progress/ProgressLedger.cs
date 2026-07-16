namespace AgentSmith.Contracts.Progress;

/// <summary>
/// p0341: the coding master's DURABLE progress ledger — a TodoWrite-style
/// checklist, seeded 1:1 from the ratified plan and kept current via the
/// update_progress tool. It is MEMORY, not a gate: it lets a long run (across the
/// p0255 apply / p0263 verdict re-drives, and under context compaction) always
/// know which plan steps are done and which remain, killing the amnesia that let
/// p0340's completion-loop lose its place. The PipelineContext copy
/// (ContextKeys.ProgressLedger) is the source of truth; the message-list copy is
/// the model's working view. Completion stays p0340's concern — the ledger never
/// fails a run.
/// </summary>
public sealed record ProgressLedger(IReadOnlyList<ProgressLedgerEntry> Entries)
{
    /// <summary>Cap on entries — the ledger is pinned/re-rendered often, so an
    /// unbounded list would eat exactly the budget compaction reclaims.</summary>
    public const int MaxItems = 40;

    /// <summary>Cap on a note's length, for the same reason.</summary>
    public const int MaxNoteLength = 500;

    public static ProgressLedger Empty { get; } = new(Array.Empty<ProgressLedgerEntry>());

    public bool IsEmpty => Entries.Count == 0;
}

/// <summary>
/// p0341: one checklist item. <paramref name="Id"/> is STABLE across
/// update_progress calls (framework-assigned for seeded steps = the plan step
/// order) so full-state replacement reconciles by id rather than guessing.
/// <paramref name="Target"/> is the repo-relative path the step touches (seeded
/// from PlanStep.TargetFile); it drives the done-status honesty DIAGNOSTIC —
/// present => cross-checked against the diff, absent => the check is skipped
/// (never a false warning). No fuzzy extraction from <paramref name="Activity"/>.
/// </summary>
public sealed record ProgressLedgerEntry(
    string Id,
    string Activity,
    ProgressStatus Status,
    string? Target = null,
    string? Note = null);

/// <summary>p0341: minimal lifecycle — pending | in_progress | done. Exactly one
/// item may be in_progress at a time (TodoWrite contract).</summary>
public enum ProgressStatus
{
    Pending,
    InProgress,
    Done,
}
