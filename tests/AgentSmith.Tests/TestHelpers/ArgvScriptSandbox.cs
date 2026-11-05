using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-11-04-6768a: a sandbox whose answers are scripted by the PREFIX of the space-joined argv, so two
/// calls of one subcommand at two refs answer differently. The first matching key in insertion order
/// wins; an unscripted command exits 0 with no output.
/// </summary>
internal sealed class ArgvScriptSandbox(IEnumerable<(string Prefix, int Exit, string Output)> script) : ISandbox
{
    private readonly List<(string Prefix, int Exit, string Output)> _script = [.. script];

    public List<string> Ran { get; } = [];

    public string JobId => "argv-script";

    public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        var argv = string.Join(' ', step.Args ?? []);
        Ran.Add(argv);
        var hit = _script.FirstOrDefault(s => argv.StartsWith(s.Prefix, StringComparison.Ordinal));
        var (exit, output) = hit.Prefix is null ? (0, string.Empty) : (hit.Exit, hit.Output);
        return Task.FromResult(new StepResult(
            StepResult.CurrentSchemaVersion, step.StepId, exit, TimedOut: false, DurationSeconds: 0.01,
            ErrorMessage: exit == 0 ? null : "scripted failure", OutputContent: output));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
