using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0196: in-memory ISandbox returning canned success for every step.
/// Records writes + run-commands so tests can assert what was emitted.
/// </summary>
internal sealed class StubSandbox : ISandbox
{
    public string JobId { get; } = "stub-" + Guid.NewGuid().ToString("N")[..8];
    public List<Step> RanSteps { get; } = new();

    public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        RanSteps.Add(step);
        var output = step.Kind switch
        {
            StepKind.ListFiles => "[]",
            StepKind.ReadFile => string.Empty,
            StepKind.WriteFile => $"File written: {step.Path}",
            _ => string.Empty,
        };
        return Task.FromResult(new StepResult(
            StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0,
            TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null, OutputContent: output));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
