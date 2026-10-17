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
    public const string RedAccepted = "red-accepted";
    public const string Verdictless = "verdictless";
    public const string Idle = "idle";
    public const string Blocked = "blocked";
    public const string SelfRefilled = "self-refilled";
    public const string Cap = "cap";
    public const string Question = "question";
    public const string Failed = "failed";

    /// <summary>On read only: a pass whose calls are stamped and whose end was never recorded.</summary>
    public const string Unknown = "unknown";

    // Re-engaged because — also what the next pass opened because.
    public const string RedWithOpenSteps = "red-with-open-steps";
    public const string UnbackedDone = "unbacked-done";
    public const string GateUnsatisfied = "gate-unsatisfied";
    /// <summary>2026-10-17-a196c: met claims whose citations resolve against nothing that changed or ran.</summary>
    public const string UnresolvedClaims = "unresolved-claims";
    public const string Challenged = "challenged";

    // Opened because, for a pass that is not a re-engagement.
    public const string First = "first";
    public const string AfterAnswer = "after-answer";
    public const string Repair = "repair";
    public const string ReviewFix = "review-fix";
    public const string StructureCorrection = "structure-correction";
}
