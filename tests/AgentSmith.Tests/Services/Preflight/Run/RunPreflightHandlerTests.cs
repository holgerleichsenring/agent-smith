using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Preflight.Run;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Preflight.Run;

/// <summary>
/// p0428: the gate aggregates its checks — failing names the lever, warning reports it,
/// and a check that crashes proves nothing rather than refusing a healthy run.
/// </summary>
public sealed class RunPreflightHandlerTests
{
    [Fact]
    public async Task AFailedCheck_StopsTheRunAndNamesTheLever()
    {
        var handler = HandlerWith(
            new StubCheck("config-loaded",
                RunPreflightFinding.Fail("config-loaded", "no agents", "pass --config")));

        var result = await handler.ExecuteAsync(new RunPreflightContext(Pipeline()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("no agents").And.Contain("pass --config");
    }

    [Fact]
    public async Task RunPreflightHandler_FirstFailureNamesKind_ResultCarriesItsKindAndDetail()
    {
        var detail = new EndDetail("Image node:22 (context default)", ["web"], "Trusted image registries", "ghcr.io/, docker.io/library/");
        var handler = HandlerWith(
            new StubCheck(ContextImageTrustCheck.CheckName,
                RunPreflightFinding.Fail(ContextImageTrustCheck.CheckName, "image outside the boundary", "trust it")
                    with { Kind = RunEndKinds.ImageUntrusted, Detail = detail }),
            new StubCheck("config-loaded", RunPreflightFinding.Fail("config-loaded", "no agents", "pass --config")));

        var result = await handler.ExecuteAsync(new RunPreflightContext(Pipeline()), CancellationToken.None);

        result.FailureKind.Should().Be(RunEndKinds.ImageUntrusted);
        result.EndDetail.Should().BeSameAs(detail);
        result.Message.Should().Contain("no agents", "every failure is still listed in the summary");
    }

    [Fact]
    public async Task RunPreflightHandler_FirstFailureNamesNoKind_ResultCarriesPreflight()
    {
        var handler = HandlerWith(
            new StubCheck("config-loaded", RunPreflightFinding.Fail("config-loaded", "no agents", "pass --config")),
            new StubCheck("bootstrap-presence", RunPreflightFinding.Fail("bootstrap-presence", "no bootstrap", "init")
                with { Kind = RunEndKinds.BootstrapMissing }));

        var result = await handler.ExecuteAsync(new RunPreflightContext(Pipeline()), CancellationToken.None);

        result.FailureKind.Should().Be(RunEndKinds.Preflight, "the first failing check decides");
        result.EndDetail.Should().BeNull();
    }

    [Fact]
    public async Task RunPreflightHandler_ConfigStep_RunsOnlyTheChecksWithoutSandbox()
    {
        var handler = HandlerWith(
            new StubCheck("config", RunPreflightFinding.Pass("config", "fine"), needsSandbox: false),
            new StubCheck("home", RunPreflightFinding.Fail("home", "not writable", "fix it")));

        var config = await handler.ExecuteAsync(new RunPreflightContext(Pipeline(), NeedsSandbox: false), CancellationToken.None);
        var sandbox = await handler.ExecuteAsync(new RunPreflightContext(Pipeline(), NeedsSandbox: true), CancellationToken.None);

        config.IsSuccess.Should().BeTrue("the sandbox check belongs to RunPreflight");
        sandbox.IsSuccess.Should().BeFalse();
        sandbox.Message.Should().Contain("not writable");
    }

    [Fact]
    public void RegisteredChecks_SplitByNeedsSandbox()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddRunPreflight();

        var sandboxed = services.Where(d => d.ServiceType == typeof(IRunPreflightCheck))
            .Select(d => d.ImplementationType!)
            .Where(t => ((IRunPreflightCheck)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(t)).NeedsSandbox)
            .Select(t => t.Name).ToList();

        sandboxed.Should().BeEquivalentTo([nameof(SandboxHomeWritableCheck), nameof(SandboxRunsAsRootCheck),
            nameof(InjectedSecretCheck), nameof(BranchStateCheck)], "only these read a sandbox or the checked-out branch");
    }

    [Fact]
    public async Task RunPreflightHandler_OtherCheckFails_ResultCarriesPreflight()
    {
        var handler = HandlerWith(
            new StubCheck("config-loaded", RunPreflightFinding.Fail("config-loaded", "no agents", "pass --config")));

        var result = await handler.ExecuteAsync(new RunPreflightContext(Pipeline()), CancellationToken.None);

        result.FailureKind.Should().Be(RunEndKinds.Preflight);
    }

    [Fact]
    public async Task EveryCheckPassing_ReportsTheSilentSentence()
    {
        var handler = HandlerWith(new StubCheck("a", RunPreflightFinding.Pass("a", "fine")));

        var result = await handler.ExecuteAsync(new RunPreflightContext(Pipeline()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        CommandStepClasses.IsNoOpSummary(CommandNames.RunPreflight, result.Message)
            .Should().BeTrue("a preflight with nothing to say must not clutter the run drawer");
    }

    [Fact]
    public async Task AWarning_IsCarriedAndTheGateStillSpeaks()
    {
        var handler = HandlerWith(
            new StubCheck("branch-state", RunPreflightFinding.Warn("branch-state", "carries 2 commits")));

        var result = await handler.ExecuteAsync(new RunPreflightContext(Pipeline()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("carries 2 commits");
        CommandStepClasses.IsNoOpSummary(CommandNames.RunPreflight, result.Message)
            .Should().BeFalse("a reported finding must be visible");
    }

    [Fact]
    public async Task ACheckThatCrashes_DoesNotFailTheRun()
    {
        var handler = HandlerWith(new ThrowingCheck());

        var result = await handler.ExecuteAsync(new RunPreflightContext(Pipeline()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("crashed");
    }

    private static RunPreflightHandler HandlerWith(params IRunPreflightCheck[] checks) =>
        new(checks, EventTestStubs.NoOp, NullLogger<RunPreflightHandler>.Instance);

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        return pipeline;
    }

    private sealed class StubCheck(string name, RunPreflightFinding finding, bool needsSandbox = true) : IRunPreflightCheck
    {
        public string Name => name;

        public bool NeedsSandbox => needsSandbox;

        public Task<RunPreflightFinding> RunAsync(PipelineContext pipeline, CancellationToken ct) =>
            Task.FromResult(finding);
    }

    private sealed class ThrowingCheck : IRunPreflightCheck
    {
        public string Name => "explodes";

        public bool NeedsSandbox => true;

        public Task<RunPreflightFinding> RunAsync(PipelineContext pipeline, CancellationToken ct) =>
            throw new InvalidOperationException("boom");
    }
}
