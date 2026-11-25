using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
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

    // 2026-11-25-9992h: a repository with a machine identity commits as the machine, whatever the image already resolves.
    [Fact]
    public async Task EnsureConfigured_WithAMachine_WritesItOverAnExistingIdentity()
    {
        var sandbox = new ScriptedSandbox(configuredEmail: "image@example.com");

        var configured = await Identity().EnsureConfiguredAsync(
            sandbox, new MachineIdentity("agent-smith-bot", "bot@example.com", "bot"), CancellationToken.None);

        configured.Should().BeTrue();
        sandbox.RanSteps.Should().NotContain(s => s.Args!.Contains("--get"), "the machine's identity is not a default to keep");
        sandbox.RanSteps.Should().Contain(s => s.Args!.Contains("user.email") && s.Args!.Contains("bot@example.com"));
        sandbox.RanSteps.Should().Contain(s => s.Args!.Contains("user.name") && s.Args!.Contains("agent-smith-bot"));
    }

    [Fact]
    public void IsOurs_TheFrameworkEmailAndTheMachineEmail_NobodyElse()
    {
        SandboxGitIdentity.IsOurs(SandboxGitIdentity.Email, null).Should().BeTrue("commits from before the machine stay ours");
        SandboxGitIdentity.IsOurs("BOT@example.com", "bot@example.com").Should().BeTrue();
        SandboxGitIdentity.IsOurs("dev@example.com", "bot@example.com").Should().BeFalse();
        SandboxGitIdentity.IsOurs(string.Empty, string.Empty).Should().BeFalse();
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
