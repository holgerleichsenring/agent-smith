using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.Services.Preflight.Run;

/// <summary>
/// 2026-08-28-b630: a sandbox that carries injected credentials, standing in for the
/// Kubernetes backend. It reads the probe script the way <c>sh</c> would — each line ends in
/// the NAME it echoes when its test fails — and echoes back the names it was not given, so
/// the assertion under test is the script the real sandbox would run, not a canned answer.
/// </summary>
internal sealed class InjectingSandbox(ResolvedSandboxSecrets injected, IReadOnlyList<string> present)
    : ISandbox, ISandboxSecretInjection
{
    public string JobId => "injecting-stub";

    public ResolvedSandboxSecrets InjectedSecrets => injected;

    /// <summary>The script the check asked this sandbox to run.</summary>
    public string Script { get; private set; } = string.Empty;

    public Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        Script = step.Args?.LastOrDefault() ?? string.Empty;
        var missing = Script
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(Echoed)
            .Where(name => !present.Contains(name, StringComparer.Ordinal));
        return Task.FromResult(new StepResult(
            StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0, TimedOut: false,
            DurationSeconds: 0.01, ErrorMessage: null,
            OutputContent: string.Join("\n", missing)));
    }

    private static string Echoed(string line) =>
        line[(line.LastIndexOf("echo ", StringComparison.Ordinal) + "echo ".Length)..].Trim('\'');

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
