using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;
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

    private sealed class StubCheck(string name, RunPreflightFinding finding) : IRunPreflightCheck
    {
        public string Name => name;

        public Task<RunPreflightFinding> RunAsync(PipelineContext pipeline, CancellationToken ct) =>
            Task.FromResult(finding);
    }

    private sealed class ThrowingCheck : IRunPreflightCheck
    {
        public string Name => "explodes";

        public Task<RunPreflightFinding> RunAsync(PipelineContext pipeline, CancellationToken ct) =>
            throw new InvalidOperationException("boom");
    }
}
