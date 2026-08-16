using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.Services.Preflight.Run;

/// <summary>
/// p0428: an ISandbox that answers one scripted stdout for every Run step, so the
/// preflight checks can be driven over a known git history without a container.
/// </summary>
internal sealed class ScriptedSandbox(string output = "", int exitCode = 0) : ISandbox
{
    public string JobId => "scripted";

    public List<Step> RanSteps { get; } = [];

    public Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        RanSteps.Add(step);
        return Task.FromResult(new StepResult(
            StepResult.CurrentSchemaVersion, step.StepId, exitCode,
            TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null, OutputContent: output));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
