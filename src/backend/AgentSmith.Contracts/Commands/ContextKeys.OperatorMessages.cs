namespace AgentSmith.Contracts.Commands;

public static partial class ContextKeys
{
    /// <summary>
    /// <c>IMidTurnMessages</c> — 2026-11-12-a1fa: what the operator wrote while this design turn runs, seeded by the
    /// server's turn runner; the master's tools hand it on with their results. Absent, nothing is delivered mid-turn.
    /// </summary>
    public const string SpecDialogOperatorMessages = "SpecDialogOperatorMessages";

    /// <summary>
    /// <c>int</c> — 2026-11-17-4860b: the transcript index of the last ticket-conversation message this code run's master
    /// was handed, so each message reaches the run once across its specs and a resume. Absent: none yet.
    /// </summary>
    public const string TicketMessagesThrough = "TicketMessagesThrough";

    /// <summary>
    /// <c>IReadOnlyList&lt;string&gt;</c> — 2026-11-17-ec48a: the texts of the ticket-conversation messages this code run
    /// was handed, oldest first, so the review of a spec the master revises reads what the operator asked for. Absent:
    /// none yet.
    /// </summary>
    public const string TicketMessagesHanded = "TicketMessagesHanded";

    /// <summary>
    /// <c>int</c> — 2026-11-17-ec48a: how many revisions of a spec the coding master had reviewed in this run, so each
    /// revision review is stored as a round of its own after the pre-build rounds. Absent: none yet.
    /// </summary>
    public const string SpecRevisionReviews = "SpecRevisionReviews";
}
