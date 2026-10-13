using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.Specs.Format;

/// <summary>
/// 2026-10-12-fbbdb: a working tree with uncommitted changes, as the spec commit meets it — the
/// gate's listings answer <see cref="Changed"/>, the checkpoint finds them staged, a shell command
/// exits <see cref="ShellExitCode"/>, and every step is recorded in order.
/// </summary>
internal sealed class FormatSandbox : ISandbox
{
    public List<string> Changed { get; } = [];

    public int ShellExitCode { get; set; }

    /// <summary>2026-10-13-a347a: what a shell command says.</summary>
    public string ShellOutput { get; set; } = string.Empty;

    public List<Step> RanSteps { get; } = [];

    public string JobId => "format-sandbox";

    public IEnumerable<string> ShellCommands =>
        RanSteps.Where(IsShell).Select(step => step.Args![1]);

    public static bool IsShell(Step step) => step.Command == "/bin/sh";

    public static bool IsGit(Step step, string verb) => step.Command == "git" && step.Args is { } a && a.Contains(verb);

    public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
    {
        RanSteps.Add(step);
        if (IsShell(step)) return Answer(step, ShellExitCode, ShellOutput);
        if (IsGit(step, "-z") && IsGit(step, "--name-only"))
            return Answer(step, 0, string.Concat(Changed.Select(path => path + "\0")) + "\n");
        if (IsGit(step, "-z") && IsGit(step, "--others")) return Answer(step, 0, "\n");
        if (IsGit(step, "--porcelain")) return Answer(step, 0, string.Concat(Changed.Select(p => $" M {p}\n")));
        if (IsGit(step, "--quiet")) return Answer(step, Changed.Count == 0 ? 0 : 1, string.Empty);
        if (IsGit(step, "--name-only")) return Answer(step, 0, string.Join('\n', Changed));
        return Answer(step, 0, string.Empty);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static Task<StepResult> Answer(Step step, int exit, string output) => Task.FromResult(
        new StepResult(StepResult.CurrentSchemaVersion, step.StepId, exit, false, 0.01, null, output));
}
