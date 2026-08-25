using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// p0504: the profile's commands reach the real gate, and the gate keeps its fail-fast —
/// a repository stops at the first non-zero exit, so one red run surfaces one gate.
/// </summary>
public sealed class VerifyPhaseDomainProfileTests
{
    private static readonly DomainProfile Profile = new(
        "sample-domain", "python:3.12-bookworm", [],
        [
            new DomainProfileCommand("parse", "tool parse"),
            new DomainProfileCommand("validate", "tool validate"),
        ]);

    [Fact]
    public async Task ResolveStages_FirstCommandFails_TheRestDoNotRun()
    {
        var (context, sandbox) = Setup(failingCommand: "tool parse");

        var result = await Handler().ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("tool parse");
        sandbox.CommandLines.Should().Contain("tool parse");
        sandbox.CommandLines.Should().NotContain("tool validate");
    }

    [Fact]
    public async Task ResolveStages_NonDotnetContextWithADomain_RunsTheProfileCommandsInOrder()
    {
        var (context, sandbox) = Setup(failingCommand: null);

        await Handler().ExecuteAsync(context, CancellationToken.None);

        sandbox.CommandLines.Where(c => c.StartsWith("tool", StringComparison.Ordinal))
            .Should().Equal("tool parse", "tool validate");
    }

    private static VerifyPhaseHandler Handler() => new(
        new VerifyStageResolver(
            new DotnetEntryPointDiscovery(
                new SandboxFileReaderFactory(), NullLogger<DotnetEntryPointDiscovery>.Instance),
            new ProfileCommandPresence(
                new SandboxFileReaderFactory(), NullLogger<ProfileCommandPresence>.Instance),
            NullLogger<VerifyStageResolver>.Instance),
        new DomainProfileStagesResolver(new TestDomainProfiles(Profile)),
        new SandboxTargets(),
        new VerifyCommandRunner(NullLogger<VerifyCommandRunner>.Instance),
        new DeliveryDiff(AgentSmith.Tests.TestHelpers.TestGit.BaseBranch, NullLogger<DeliveryDiff>.Instance),
        new PhaseAccounting(
            new DeliveryDiff(AgentSmith.Tests.TestHelpers.TestGit.BaseBranch, NullLogger<DeliveryDiff>.Instance),
            new SpecAccountant(
                new ScriptedChatClientFactory(),
                new AccountCalls(new SpecAccountCall(
                    new ScriptedChatClientFactory(), new AsyncLocalRunContextAccessor(),
                    NullLogger<SpecAccountCall>.Instance)),
                NullLogger<SpecAccountant>.Instance),
            new SandboxTargets(),
            NullLogger<PhaseAccounting>.Instance),
        new PhaseProgressRecorder(new NoOpEventPublisher()),
        NullLogger<VerifyPhaseHandler>.Instance);

    private static (VerifyPhaseContext Context, ScriptedSandbox Sandbox) Setup(string? failingCommand)
    {
        var pipeline = new PipelineContext();
        var sandbox = new ScriptedSandbox(failingCommand);
        var discovery = new RemoteContextDiscovery("warehouse", ".", "python", Domain: "sample-domain");
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["data"] = sandbox });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery> { ["data"] = discovery });
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>> { ["data"] = [discovery] });
        var map = new ProjectMap(
            "python", [], [], [], [], new Conventions(null, null, null),
            new CiConfig(false, null, null, null));
        return (
            new VerifyPhaseContext(new Dictionary<string, ProjectMap> { ["data"] = map }, pipeline),
            sandbox);
    }

    private sealed class ScriptedSandbox(string? failingCommand) : ISandbox
    {
        public string JobId => "verify-domain-test";

        public List<string> CommandLines { get; } = [];

        public Task<StepResult> RunStepAsync(
            Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            var line = step.Command == "/bin/sh" && step.Args is { Count: 2 } args
                ? args[1] : step.Command ?? string.Empty;
            CommandLines.Add(line);
            var isGit = step.Command == "git";
            var output = isGit
                ? "diff --git a/warehouse/model.sql b/warehouse/model.sql\n"
                  + "--- a/warehouse/model.sql\n+++ b/warehouse/model.sql\n@@ -1 +1 @@\n+changed\n"
                : string.Empty;
            var failed = failingCommand is not null
                && line.Contains(failingCommand, StringComparison.Ordinal);
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, ExitCode: failed ? 1 : 0,
                TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null, OutputContent: output));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
