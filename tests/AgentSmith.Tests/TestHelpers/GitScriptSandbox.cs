using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-10-13-3890b: a sandbox whose git answers from a script — origin/HEAD names main, the
/// branch's name-status and untracked list are canned, and an unreadable one fails every call.
/// </summary>
public sealed class GitScriptSandbox(
    string nameStatus, string untracked = "", bool readable = true, string jobId = "git-script") : ISandbox
{
    public string JobId => jobId;

    public IReadOnlyList<string> DiffArgs { get; private set; } = [];

    public static GitScriptSandbox Unreadable(string jobId = "unreadable") => new("", readable: false, jobId: jobId);

    public Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        var args = step.Args ?? [];
        if (args.Contains("diff")) DiffArgs = args;
        var output = args switch
        {
            _ when args.Contains("symbolic-ref") => "origin/main\n",
            _ when args.Contains("diff") => nameStatus,
            _ when args.Contains("ls-files") => untracked,
            _ => string.Empty,
        };
        return Task.FromResult(new StepResult(
            StepResult.CurrentSchemaVersion, step.StepId, ExitCode: readable ? 0 : 128, TimedOut: false,
            DurationSeconds: 0.01, ErrorMessage: null, OutputContent: output));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
