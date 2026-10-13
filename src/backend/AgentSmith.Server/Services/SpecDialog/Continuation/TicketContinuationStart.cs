namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-9a59m: when the attempt an approval asks for starts — <see cref="TicketStartArms"/> — and the
/// run it names (the live run it waits for, the hand-back it answers, the question that waits).
/// </summary>
public sealed record TicketContinuationStart(string Kind, string? RunId = null)
{
    /// <summary>The approval is an operator act: it starts an attempt now, after the live run, or by
    /// answering the hand-back. A refused or permission-less approval starts nothing, ever.</summary>
    public bool StartsAttempt => Kind is TicketStartArms.StartNow or TicketStartArms.WaitForRun or TicketStartArms.AnswersHandback;
}

/// <summary>2026-10-13-9a59m: the start arms of an approval in a ticket's own conversation.</summary>
public static class TicketStartArms
{
    public const string StartNow = "start-now";
    public const string WaitForRun = "wait-for-run";
    public const string QuestionPending = "refused-question-pending";
    public const string WithoutRunsControl = "recorded-without-runs-control";
    public const string AnswersHandback = "answers-handback";
}

/// <summary>
/// 2026-10-13-9a59m: what the start arm is decided over, read with the rest of the state — whether the
/// approver may start runs, the run holding the ticket's lease, the newest code attempt, a queued code
/// reservation, and whether that attempt waits on a checkpointed question.
/// </summary>
public sealed record TicketStartInputs(
    bool MayStartRuns, string? LiveRunId, string? AttemptRunId, string? AttemptStatus, bool HasQueued, bool Checkpointed);
