using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

// p0411: the committing identity is framework-owned. It is set once where a sandbox
// gets its repo; every later caller (staging, commit, checkpoint) probes instead of
// re-writing it, and a sandbox that already resolves an identity keeps it.
public sealed class SandboxGitIdentityTests
{
    private static SandboxGitIdentity Identity() =>
        new(NullLogger<SandboxGitIdentity>.Instance);

    [Fact]
    public async Task EnsureConfigured_NoIdentityYet_SetsNameAndEmail()
    {
        var sandbox = new ScriptedSandbox(configuredEmail: string.Empty);

        var configured = await Identity().EnsureConfiguredAsync(sandbox, CancellationToken.None);

        configured.Should().BeTrue();
        sandbox.RanSteps.Should().Contain(s =>
            s.Args!.Contains("user.email") && s.Args!.Contains(SandboxGitIdentity.Email));
        sandbox.RanSteps.Should().Contain(s =>
            s.Args!.Contains("user.name") && s.Args!.Contains(SandboxGitIdentity.Name));
    }

    [Fact]
    public async Task EnsureConfigured_IdentityAlreadyResolves_LeavesItAlone()
    {
        // A bind-mounted local repo carries the operator's own git config — overwriting
        // it would put this system's name on their commits.
        var sandbox = new ScriptedSandbox(configuredEmail: "dev@example.com");

        var configured = await Identity().EnsureConfiguredAsync(sandbox, CancellationToken.None);

        configured.Should().BeFalse();
        sandbox.RanSteps.Should().ContainSingle().Which.Args.Should().Contain("--get");
    }

    private sealed class ScriptedSandbox(string configuredEmail) : ISandbox
    {
        public string JobId => "identity-test";
        public List<Step> RanSteps { get; } = new();

        public Task<StepResult> RunStepAsync(
            Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            RanSteps.Add(step);
            var isProbe = step.Args?.Contains("--get") == true;
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0,
                TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null,
                OutputContent: isProbe ? configuredEmail : string.Empty));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
