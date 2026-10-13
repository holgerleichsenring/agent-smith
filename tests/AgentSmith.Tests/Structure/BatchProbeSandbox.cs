using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.Structure;

/// <summary>
/// 2026-10-13-ee83: records every step a batched read sends and, when <see cref="Answer"/> is set,
/// answers the batch step itself (exit code and stdout) instead of the sandbox it wraps.
/// </summary>
internal sealed class BatchProbeSandbox(ISandbox inner) : ISandbox
{
    public (int ExitCode, string? Output)? Answer { get; set; }
    public List<Step> Steps { get; } = [];
    public string JobId => inner.JobId;

    public int BatchSteps => Steps.Count(s => s.Kind == StepKind.Run && s.Command == "sh");
    public int ReadSteps => Steps.Count(s => s.Kind == StepKind.ReadFile);

    public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
    {
        Steps.Add(step);
        if (Answer is not { } answer || step.Command != "sh") return inner.RunStepAsync(step, progress, ct);
        return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, answer.ExitCode, false, 0.01, null, answer.Output));
    }

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
