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

    /// <summary>
    /// p0466: the derived spec the executing step belongs to, or null outside any
    /// spec. Producer knowledge, exactly as the step index is — the runner knows the
    /// spec because it dispatches the command that carries it, and nothing downstream
    /// re-derives it from a label.
    /// </summary>
    string? CurrentSpecId { get; }

    /// <summary>
    /// p0388a: opens the ambient step frame for <paramref name="stepIndex"/>.
    /// p0466: <paramref name="specId"/> is the spec that step belongs to.
    /// </summary>
    IDisposable BeginStepScope(int stepIndex, string? specId = null);

    /// <summary>
    /// 2026-10-13-9a59f: the run-wide ordinal of the master pass executing on this flow, or null outside
    /// one. Default members, so an accessor that keeps no pass frame reads null and opens nothing.
    /// </summary>
    int? CurrentMasterPass => null;

    /// <summary>2026-10-13-9a59f: opens the ambient master-pass frame. Open it synchronously in the
    /// method that runs the pass: a frame set inside an async callee is gone when the callee returns.</summary>
    IDisposable BeginMasterPassScope(int ordinal) => NoFrame.Instance;

    private sealed class NoFrame : IDisposable
    {
        public static readonly NoFrame Instance = new();

        public void Dispose()
        {
        }
    }
}
