using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-11-25-9992b: a backend that answers a step by the longest scripted prefix of its space-joined command line
/// (exit code and output), exit 0 with nothing otherwise, and remembers every step as it arrived.
/// </summary>
internal sealed class ScriptedGitSandbox : ISandbox
{
    private readonly Dictionary<string, (int ExitCode, string Output)> _answers = new(StringComparer.Ordinal);

    public List<Step> Steps { get; } = [];

    public string JobId => "scripted";

    public IReadOnlyList<string> Lines => [.. Steps.Select(Line)];

    public ScriptedGitSandbox Answer(string prefix, int exitCode, string output = "")
    {
        _answers[prefix] = (exitCode, output);
        return this;
    }

    public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        Steps.Add(step);
        var line = Line(step);
        var hit = _answers.Where(a => line.StartsWith(a.Key, StringComparison.Ordinal))
            .OrderByDescending(a => a.Key.Length).Select(a => a.Value).FirstOrDefault((ExitCode: 0, Output: string.Empty));
        return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, hit.ExitCode, false, 0.1,
            hit.ExitCode == 0 ? null : hit.Output, hit.Output));
    }

    // The credential helper a remote-facing git step carries is not part of what it does.
    private static string Line(Step step) =>
        string.Join(' ', new[] { step.Command ?? string.Empty }.Concat((step.Args ?? []).Where(a => !a.StartsWith("credential.", StringComparison.Ordinal) && a != "-c")));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
