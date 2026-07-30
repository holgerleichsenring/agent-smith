namespace AgentSmith.Contracts.Events;

/// <summary>
/// Ambient handle on the current run + active LLM-call attribution. The
/// runId scope is opened by <c>ExecutePipelineUseCase</c> at pipeline
/// start; the call scope is opened by handlers around each
/// <c>.GetResponseAsync</c> invocation. Both are AsyncLocal-backed so
/// cross-cutting decorators (chat client, AI function) read the right
/// frame on every event without ctor plumbing.
///
/// <para>p0388a: the STEP scope is the third frame — opened by
/// <c>PipelineStepRunner</c> around each step's execution so every event
/// published inside it (including sub-agent work on child tasks) carries the
/// step it belongs to. Step membership is producer knowledge; nothing
/// downstream re-derives it.</para>
/// </summary>
public interface IRunContextAccessor
{
    string? CurrentRunId { get; }
    CallScope? CurrentCallScope { get; }

    /// <summary>
    /// p0388a: the index of the pipeline step currently executing on this async
    /// flow, or null outside any step (run setup, teardown, server-side work).
    /// </summary>
    int? CurrentStepIndex { get; }

    IDisposable BeginScope(string runId);
    IDisposable BeginCallScope(string role, string phase, string? repoName = null);

    /// <summary>p0388a: opens the ambient step frame for <paramref name="stepIndex"/>.</summary>
    IDisposable BeginStepScope(int stepIndex);
}
