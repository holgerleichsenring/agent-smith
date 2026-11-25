using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-11-25-9992c: a backend that declares no root and answers every step with exit 0, remembering each step as
/// it arrived — so a test can read what a <c>RepoSandbox</c> above it handed down. <c>rev-parse</c> answers a sha;
/// everything else answers nothing.
/// </summary>
internal sealed class StepRecordingSandbox : ISandbox
{
    public const string Head = "0123456789abcdef0123456789abcdef01234567";

    public List<Step> Steps { get; } = [];

    public string JobId => "recording";

    public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        Steps.Add(step);
        var output = step.Args?.Contains("rev-parse") == true ? Head : string.Empty;
        return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, output));
    }

    public IReadOnlyList<Step> Git => [.. Steps.Where(s => s.Command == "git")];

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
