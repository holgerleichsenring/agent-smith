using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-01-379a: the target is asked whether it answers before the master spends a token,
/// and the three outcomes stay distinguishable in the record.
/// </summary>
public sealed class TargetProbeTests
{
    private const string Command = "sf org display --target-org devhub";
    private const string Target = "the warehouse dev workspace";

    // What a CLI prints when it cannot authenticate: the credential it was handed, in
    // clear. The masker only knows values the framework holds and never holds this one.
    private const string Leaky = "ERROR: no auth for user svc-loader@example.invalid (token gAAAAA)";

    [Fact]
    public async Task Probe_ARefusingTarget_FailsBeforeTheMasterRuns()
    {
        var result = await Ask(new AnsweringSandbox(exitCode: 1, Leaky));

        result.IsSuccess.Should().BeFalse("a target that refuses is not a run that may proceed");
        result.Message.Should().Contain(Target).And.Contain(Command).And.Contain("exited 1");

        var code = PipelinePresets.Code.ToList();
        code.IndexOf(CommandNames.ProbeTarget).Should()
            .BeGreaterThan(code.IndexOf(CommandNames.EnsurePrerequisites),
                "the prerequisite step installs the CLI the probe calls")
            .And.BeLessThan(code.IndexOf(CommandNames.PhaseSequence),
                "the masters are spliced in by PhaseSequence — a refusal must cost no token");
    }

    [Fact]
    public async Task Probe_ARefusingTarget_ReportsNoCapturedOutput()
    {
        var result = await Ask(new AnsweringSandbox(exitCode: 4, Leaky));

        result.Message.Should().NotContain("svc-loader").And.NotContain("gAAAAA")
            .And.NotContain(Leaky,
                "a failure reason reaches the result document and a ticket comment, and the "
                + "masker cannot replace a value the framework never held");
        result.Message.Should().Contain("exited 4", "the exit code is what it may carry");
    }

    [Fact]
    public async Task Probe_NoProbeDeclared_IsRecordedAsNotDeclaredNotAsPassed()
    {
        var result = await Run(Pipeline(new AnsweringSandbox(exitCode: 0), probe: null));

        result.IsSuccess.Should().BeTrue("a repository that declares no probe still runs");
        result.Message.Should().Contain("No target probe is declared");
        CommandStepClasses.IsNoOpSummary(CommandNames.ProbeTarget, result.Message)
            .Should().BeFalse("a repository nothing asked about must not read like one that answered");
    }

    [Fact]
    public async Task Probe_ABackendThatInjectsNothing_SkipsAndSaysSo()
    {
        var sandbox = new StubSandbox();

        var result = await Run(Pipeline(sandbox, Probe));

        result.IsSuccess.Should().BeTrue(
            "docker and in-process sandboxes carry no secrets — failing there would redden "
            + "every harness run of a repository that declares a probe");
        result.Message.Should().Contain("injects no credentials").And.Contain(Target);
        sandbox.RanSteps.Should().BeEmpty("an unanswerable question is not asked");
        CommandStepClasses.IsNoOpSummary(CommandNames.ProbeTarget, result.Message)
            .Should().BeFalse("a skipped probe is reported, never silently green");
    }

    [Fact]
    public async Task Probe_AnAnsweringTarget_IsTheOnlyOutcomeTheGateHides()
    {
        var result = await Ask(new AnsweringSandbox(exitCode: 0));

        result.IsSuccess.Should().BeTrue();
        CommandStepClasses.IsNoOpSummary(CommandNames.ProbeTarget, result.Message)
            .Should().BeTrue("the answered sentence is the step's one silent outcome");
    }

    [Fact]
    public async Task Probe_TheDeclaredCommand_RunsThroughAShellAtTheRepoRoot()
    {
        var sandbox = new AnsweringSandbox(exitCode: 0);

        await Ask(sandbox);

        var step = sandbox.RanSteps.Should().ContainSingle().Subject;
        step.Command.Should().Be("/bin/sh", "an injected credential is reached as $VAR");
        step.Args.Should().Equal(["-c", Command]);
        step.WorkingDirectory.Should().Be("/work",
            "2026-09-03-7bac: a probe runs where every other command runs; the declaring "
            + "context's meta.workdir places nothing");
    }

    private static readonly ContextYamlTargetProbe Probe = new(Target, Command);

    private static Task<CommandResult> Ask(ISandbox sandbox) => Run(Pipeline(sandbox, Probe));

    private static async Task<CommandResult> Run(PipelineContext pipeline) =>
        await new ProbeTargetHandler(
                new ContextTargetProbeResolver(),
                new TargetProbeRunner(NullLogger<TargetProbeRunner>.Instance),
                NullLogger<ProbeTargetHandler>.Instance)
            .ExecuteAsync(new ProbeTargetContext(pipeline), CancellationToken.None);

    private static PipelineContext Pipeline(ISandbox sandbox, ContextYamlTargetProbe? probe)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["warehouse"] = sandbox });
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal)
            {
                ["warehouse"] =
                [
                    new RemoteContextDiscovery("warehouse", "warehouse", "python", Probe: probe)
                ],
            });
        return pipeline;
    }
}
