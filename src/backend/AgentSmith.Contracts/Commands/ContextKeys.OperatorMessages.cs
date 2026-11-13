namespace AgentSmith.Contracts.Commands;

public static partial class ContextKeys
{
    /// <summary>
    /// <c>IMidTurnMessages</c> — 2026-11-12-a1fa: what the operator wrote while this design turn runs, seeded by the
    /// server's turn runner; the master's tools hand it on with their results. Absent, nothing is delivered mid-turn.
    /// </summary>
    public const string SpecDialogOperatorMessages = "SpecDialogOperatorMessages";
}
