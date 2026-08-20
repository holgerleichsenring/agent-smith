using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

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
    string repo,
    ILogger? logger = null) : ISandbox, ISandboxLivenessProbeTarget
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
        WarnIfStreamFellBehind(step, result, tail, summary);
    }

    /// <summary>
    /// p0491: a run step whose stream carried less than its result body is this defect's
    /// signature — the drawer is showing an output the command did not stop at. The model
    /// reads the body now, so this costs a run nothing; a persistent gap means the event
    /// drain is losing entries again.
    /// </summary>
    private void WarnIfStreamFellBehind(
        Step step, StepResult? result, OutputTailBuffer tail, string? summary)
    {
        if (step.Kind != StepKind.Run) return;
        var body = result?.OutputContent?.Length ?? 0;
        if (body <= tail.TotalChars) return;
        logger?.LogWarning(
            "Sandbox output stream fell behind for {Repo}: streamed {Streamed} of {Body} "
            + "characters for `{Command}` — the drawer is missing lines the command printed",
            repo, tail.TotalChars, body, Shorten(summary));
    }

    private static string Shorten(string? summary) =>
        summary is { Length: > 80 } text ? text[..80] + "…" : summary ?? string.Empty;

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
