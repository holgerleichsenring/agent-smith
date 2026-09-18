using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-09-17-042eh: a sandbox whose git answers are scripted per SUBCOMMAND, so a test can
/// make one command fail while the rest succeed. The stub sandbox answers exit 0 to
/// everything, which makes the failure paths of anything that shells out to git unreachable.
/// </summary>
internal sealed class ScriptedGitSandbox(
    IReadOnlyDictionary<string, (int Exit, string Output)> byArgument) : ISandbox
{
    public List<IReadOnlyList<string>> RanArgs { get; } = [];

    public string JobId => "scripted-git";

    public Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        var args = step.Args ?? [];
        RanArgs.Add(args);
        var scripted = byArgument.FirstOrDefault(entry => args.Contains(entry.Key));
        var (exit, output) = scripted.Key is null ? (0, string.Empty) : scripted.Value;
        return Task.FromResult(new StepResult(
            StepResult.CurrentSchemaVersion, step.StepId, exit, TimedOut: false,
            DurationSeconds: 0.01, ErrorMessage: exit == 0 ? null : "scripted failure",
            OutputContent: output));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
