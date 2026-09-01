using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-01-379a: a sandbox that carries injected credentials and answers a probe with a
/// chosen exit code, standing in for the Kubernetes backend.
/// <para>
/// Its output deliberately looks like the worst case: a CLI that echoes the credential it
/// was handed. Nothing the framework holds can mask that value, so the assertion the tests
/// make is that it never leaves this class.
/// </para>
/// </summary>
internal sealed class AnsweringSandbox(int exitCode, string output = "") : ISandbox, ISandboxSecretInjection
{
    private static readonly SecretRef Source = new("target-creds", "username");

    public string JobId => "answering-stub";

    public ResolvedSandboxSecrets InjectedSecrets { get; } =
        new([new SecretEnvBinding("TARGET_USERNAME", Source)], []);

    /// <summary>Every step the probe asked this sandbox to run.</summary>
    public List<Step> RanSteps { get; } = [];

    public Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        RanSteps.Add(step);
        return Task.FromResult(new StepResult(
            StepResult.CurrentSchemaVersion, step.StepId, exitCode, TimedOut: false,
            DurationSeconds: 0.01, ErrorMessage: null, OutputContent: output));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
