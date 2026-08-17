using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Events;

/// <summary>
/// Wraps an <see cref="ISandbox"/> so each RunStepAsync invocation emits
/// L3 events: SandboxCommand on entry, SandboxOutput per Stdout/Stderr
/// StepEvent (with batchSeq per call), SandboxResult on completion.
/// Sandbox.Agent + Sandbox.Wire stay untouched; the agent-side stream
/// (sandbox:{jobId}:events) is already consumed by the underlying sandbox
/// impl which forwards each StepEvent into the IProgress callback — this
/// decorator is the deployment-seam projection layer.
/// </summary>
public sealed class SandboxEventProjector(
    ISandbox inner,
    IEventPublisher eventPublisher,
    IRunContextAccessor runContext,
    string repo) : ISandbox, ISandboxLivenessProbeTarget
{
    // p0357: flags tree-mutating commands so the dashboard's write counter is honest
    // about script edits. Pure classifier; one instance per projector.
    private readonly MutatingCommandClassifier _writeClassifier = new();

    public string JobId => inner.JobId;

    // p0201: surface the underlying liveness probe target id (if any) so the
    // coordinator doesn't have to peel the projector wrapper. Empty string when
    // the inner sandbox doesn't implement the marker (InProcess / Kubernetes).
    public string LivenessProbeTargetId => inner is ISandboxLivenessProbeTarget t
        ? t.LivenessProbeTargetId
        : string.Empty;

    public async Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        var runId = runContext.CurrentRunId;
        if (string.IsNullOrEmpty(runId))
            return await inner.RunStepAsync(step, progress, cancellationToken);

        var commandLabel = step.Command ?? step.Kind.ToString();
        var argsLength = SandboxStepFacts.ArgsLength(step);
        var summary = SandboxStepFacts.Summarize(step);

        await eventPublisher.PublishAsync(
            new SandboxCommandEvent(
                runId!, repo, commandLabel, argsLength, DateTimeOffset.UtcNow, summary,
                IsWrite: _writeClassifier.IsMutating(step)),
            cancellationToken);

        var startedAt = DateTimeOffset.UtcNow;
        long batchSeq = 0;
        // p0367: the same progress stream that fans per-line output to the sandbox
        // drawer also feeds a bounded tail buffer, so a FAILED command's last lines
        // ride out on the (persisted) SandboxResult without persisting every line.
        var tail = new OutputTailBuffer();
        var wrapped = new ProjectingProgress(
            progress, eventPublisher, runId!, repo, () => Interlocked.Increment(ref batchSeq), tail);

        StepResult? result = null;
        try
        {
            result = await inner.RunStepAsync(step, wrapped, cancellationToken);
            return result;
        }
        finally
        {
            await PublishResultAsync(
                runId!, commandLabel, summary, step, result, startedAt, tail, argsLength);
        }
    }

    private async Task PublishResultAsync(
        string runId, string commandLabel, string? summary, Step step,
        StepResult? result, DateTimeOffset startedAt, OutputTailBuffer tail, int argsLength)
    {
        var durationMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
        var exitCode = result?.ExitCode ?? -1;
        // Attach the compact tail only on failure — a healthy command needs no
        // durable output, and success stays a single lightweight result row.
        var outputTail = exitCode != 0 ? tail.Render() : null;
        try
        {
            await eventPublisher.PublishAsync(
                new SandboxResultEvent(runId, repo, commandLabel, exitCode,
                    durationMs, DateTimeOffset.UtcNow, outputTail, summary,
                    SandboxStepFacts.ContentHash(step, result),
                    // p0423: what the command printed, and how much of it the caller was
                    // handed. A build that streamed four megabytes into a result nobody
                    // kept is invisible from either number alone.
                    argsLength, tail.TotalChars, result?.OutputContent?.Length ?? 0),
                CancellationToken.None);
        }
        catch { /* publisher failure must not mask the inner exception */ }
    }

    public ValueTask DisposeAsync() => inner.DisposeAsync();

    private sealed class ProjectingProgress(
        IProgress<StepEvent>? upstream,
        IEventPublisher eventPublisher,
        string runId,
        string repo,
        Func<long> nextSeq,
        OutputTailBuffer tail) : IProgress<StepEvent>
    {
        public void Report(StepEvent value)
        {
            upstream?.Report(value);
            var seq = nextSeq();
            var outputEvent = StepEventToRunEventMapper.AsOutput(value, runId, repo, seq);
            if (outputEvent is null) return;
            // p0367: retain the line in the bounded tail for a possible failure capture.
            tail.Add(outputEvent.Line);
            // Fire-and-forget: IProgress.Report is synchronous; we mustn't block
            // the sandbox thread. Errors are swallowed (publisher logs them).
            _ = eventPublisher.PublishAsync(outputEvent, CancellationToken.None);
        }
    }
}
