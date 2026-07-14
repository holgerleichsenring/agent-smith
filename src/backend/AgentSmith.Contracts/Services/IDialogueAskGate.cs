using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0327: the hybrid human-wait primitive. Publishes a DialogQuestion, waits
/// hot in-memory up to the configured threshold, and past it checkpoints an
/// eligible ticket run (persist cursor + context via the event stream, mark
/// the pipeline WaitingForInput) so the worker task can end. A resumed run's
/// delivered answer is consumed here instead of re-asking.
/// </summary>
public interface IDialogueAskGate
{
    Task<DialogueAskOutcome> AskAsync(
        PipelineContext pipeline, DialogQuestion question, CancellationToken cancellationToken);
}

/// <summary>
/// Either an answer (live, resumed, or the timeout/no-transport default) or
/// <c>Checkpointed = true</c> — the run parked and the caller must return a
/// success result so the executor unwinds cleanly (sandboxes released).
/// </summary>
public sealed record DialogueAskOutcome(DialogAnswer? Answer, bool Checkpointed)
{
    public static DialogueAskOutcome Answered(DialogAnswer answer) => new(answer, false);
    public static DialogueAskOutcome Parked() => new(null, true);
}
