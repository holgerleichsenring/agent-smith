namespace AgentSmith.Contracts.Commands;

public static partial class ContextKeys
{
    /// <summary>
    /// <c>DialogThread</c> — the thread a dialog turn continues, seeded by the server's turn runner when the
    /// conversation holds one for this turn; absent, the turn renders the transcript.
    /// </summary>
    public const string SpecDialogThread = "SpecDialogThread";
}
