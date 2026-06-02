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
        var argsLength = EstimateArgsLength(step);
        var summary = BuildSummary(step);

        await eventPublisher.PublishAsync(
            new SandboxCommandEvent(runId!, repo, commandLabel, argsLength, DateTimeOffset.UtcNow, summary),
            cancellationToken);

        var startedAt = DateTimeOffset.UtcNow;
        long batchSeq = 0;
        var wrapped = new ProjectingProgress(
            progress, eventPublisher, runId!, repo, () => Interlocked.Increment(ref batchSeq));

        StepResult? result = null;
        try
        {
            result = await inner.RunStepAsync(step, wrapped, cancellationToken);
            return result;
        }
        finally
        {
            await PublishResultAsync(runId!, commandLabel, result, startedAt);
        }
    }

    private async Task PublishResultAsync(
        string runId, string commandLabel, StepResult? result, DateTimeOffset startedAt)
    {
        var durationMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
        try
        {
            await eventPublisher.PublishAsync(
                new SandboxResultEvent(runId, repo, commandLabel, result?.ExitCode ?? -1,
                    durationMs, DateTimeOffset.UtcNow),
                CancellationToken.None);
        }
        catch { /* publisher failure must not mask the inner exception */ }
    }

    public ValueTask DisposeAsync() => inner.DisposeAsync();

    private static int EstimateArgsLength(Step step)
    {
        var argsLength = 0;
        if (step.Args is { Count: > 0 })
            argsLength = step.Args.Sum(a => a?.Length ?? 0);
        if (step.Content is not null) argsLength += step.Content.Length;
        return argsLength;
    }

    // p0175-fix: one-liner for the activity row. Uses only structured
    // fields (Path, Pattern, first 1-2 Args) — never the Content blob or
    // Env/secrets. Capped at 120 chars to stay readable in a row.
    private const int SummaryCap = 120;
    private static string? BuildSummary(Step step) => step.Kind switch
    {
        StepKind.Run => FromArgs(step.Args),
        StepKind.ReadFile or StepKind.WriteFile or StepKind.ListFiles or StepKind.DirectoryTree
            => Trim(step.Path),
        StepKind.Grep => string.IsNullOrEmpty(step.Pattern)
            ? Trim(step.Path)
            : Trim($"{step.Pattern} in {step.Path}"),
        _ => null,
    };

    private static string? FromArgs(IReadOnlyList<string>? args)
    {
        if (args is null || args.Count == 0) return null;
        var firstTwo = args.Take(2).Where(a => !string.IsNullOrEmpty(a));
        return Trim(string.Join(' ', firstTwo));
    }

    private static string? Trim(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Length > SummaryCap ? value[..SummaryCap] : value;
    }

    private sealed class ProjectingProgress(
        IProgress<StepEvent>? upstream,
        IEventPublisher eventPublisher,
        string runId,
        string repo,
        Func<long> nextSeq) : IProgress<StepEvent>
    {
        public void Report(StepEvent value)
        {
            upstream?.Report(value);
            var seq = nextSeq();
            var outputEvent = StepEventToRunEventMapper.AsOutput(value, runId, repo, seq);
            if (outputEvent is null) return;
            // Fire-and-forget: IProgress.Report is synchronous; we mustn't block
            // the sandbox thread. Errors are swallowed (publisher logs them).
            _ = eventPublisher.PublishAsync(outputEvent, CancellationToken.None);
        }
    }
}
