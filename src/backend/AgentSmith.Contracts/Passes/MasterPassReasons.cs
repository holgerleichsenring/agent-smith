namespace AgentSmith.Contracts.Passes;

/// <summary>
/// 2026-10-13-9a59f: the wire vocabulary of a master pass — why it ended, why the loop re-engaged it,
/// and why it opened. The words are what the master's loop decided, never inferred afterwards.
/// </summary>
public static class MasterPassReasons
{
    // Ended because.
    public const string Reengaged = "re-engaged";
    public const string NothingLeft = "nothing-left";
    public const string Budget = "budget";
    /// <summary>2026-11-22-96c8a: words of stored passes only — no pass ends on a closing-tool verdict any more.</summary>
    public const string RedAccepted = "red-accepted";
    public const string Verdictless = "verdictless";
    /// <summary>2026-11-22-96c8a: actionable checklist steps were still open after their one re-drive.</summary>
    public const string StepsOpen = "steps-open";
    public const string Idle = "idle";
    public const string Blocked = "blocked";
    public const string SelfRefilled = "self-refilled";
    public const string Cap = "cap";
    public const string Question = "question";
    public const string Failed = "failed";

    /// <summary>On read only: a pass whose calls are stamped and whose end was never recorded.</summary>
    public const string Unknown = "unknown";

    // Re-engaged because — also what the next pass opened because.
    /// <summary>2026-11-22-96c8a: a word of stored passes only.</summary>
    public const string RedWithOpenSteps = "red-with-open-steps";
    public const string UnbackedDone = "unbacked-done";
    /// <summary>2026-11-22-96c8a: the master ended with actionable checklist steps open; it is driven once more for them.</summary>
    public const string Unfinished = "unfinished";

    // Opened because, for a pass that is not a re-engagement.
    public const string First = "first";
    public const string AfterAnswer = "after-answer";
    public const string Repair = "repair";
    public const string ReviewFix = "review-fix";
    public const string StructureCorrection = "structure-correction";
}
