using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// A full clone that can also be pushed to: it answers ref checks from the branches it
/// knows, and a successful push teaches it the branch it just created.
/// </summary>
internal sealed class RungPushSandbox(string rung, params string[] remoteBranches) : ISandbox
{
    private readonly List<string> _branches = [.. remoteBranches];

    public List<IReadOnlyList<string>> Ran { get; } = [];

    public string JobId => "slice";

    public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
    {
        var args = step.Args ?? [];
        Ran.Add(args);
        var (exit, output) = Answer(args);
        return Task.FromResult(new StepResult(
            StepResult.CurrentSchemaVersion, step.StepId, exit, false, 0.1, null, output));
    }

    public IReadOnlyList<string>? Pushed => Ran.FirstOrDefault(a => a.Contains("push"));

    private (int Exit, string Output) Answer(IReadOnlyList<string> args)
    {
        if (args.Contains("symbolic-ref")) return (0, "origin/main");
        if (args.Contains("push"))
        {
            _branches.Add(rung);
            return (0, string.Empty);
        }
        if (args.Contains("fetch")) return (0, string.Empty);
        var wanted = args[^1];
        return _branches.Any(b => wanted == $"refs/remotes/origin/{b}")
            ? (0, "deadbeef")
            : (1, string.Empty);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
