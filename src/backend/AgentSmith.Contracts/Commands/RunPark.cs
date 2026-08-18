namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0440: is this run WAITING for the operator, whichever way it came to be waiting?
/// <para>
/// Two things park a run and they are set by different machinery: the master asking a
/// mid-run question (p0315d, <see cref="ContextKeys.OpenQuestionsAwaitingAnswer"/>) and the
/// dialogue ask gate checkpointing (p0327, <see cref="ContextKeys.WaitingForInput"/>).
/// ExecutePipelineUseCase knew only the second, so a run that asked the operator a question
/// was recorded as <c>success</c> — the run list showed it Done, "Needs you" showed zero,
/// and the question sat on the ticket with nothing pointing at it.
/// </para>
/// <para>
/// One predicate, so a third way to park cannot be added to one half of the system.
/// </para>
/// </summary>
public static class RunPark
{
    public static bool IsWaitingForOperator(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return Flag(pipeline, ContextKeys.WaitingForInput)
            || Flag(pipeline, ContextKeys.OpenQuestionsAwaitingAnswer);
    }

    private static bool Flag(PipelineContext pipeline, string key) =>
        pipeline.TryGet<bool>(key, out var set) && set;
}
