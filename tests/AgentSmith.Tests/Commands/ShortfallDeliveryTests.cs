using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// p0439: what a shortfall run delivers and how it says so. Delivery is refused when a
/// sandbox cannot be brought back to its verified state — the keystone stays.
/// </summary>
public sealed class ShortfallDeliveryTests
{
    private static readonly SpecSequenceProgress OneDoneOneOpen = new(
    [
        new PhaseProgress("p0001a", "Introduce the guard", PhaseRunState.Done),
        new PhaseProgress("p0001b", "Move the callers", PhaseRunState.InProgress),
    ]);

    private readonly ShortfallDelivery _sut = new(
        new UnverifiedWorkReverter(
            new SandboxGitOperations(
                new GitBranchPusher(), NullLogger<SandboxGitOperations>.Instance,
                new StubSandboxFileReaderFactory(), new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance)),
            NullLogger<UnverifiedWorkReverter>.Instance),
        NullLogger<ShortfallDelivery>.Instance);

    [Fact]
    public async Task Prepare_NoVerifiedPhase_IsNull()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.FailureReason, "budget");

        var shortfall = await _sut.PrepareAsync(pipeline, [("server", new StubSandbox())], CancellationToken.None);

        shortfall.Should().BeNull();
    }

    [Fact]
    public async Task Prepare_AVerifiedPhaseWithItsHeadRecorded_IsTheShortfall()
    {
        var pipeline = Pipeline(withHead: true);

        var shortfall = await _sut.PrepareAsync(pipeline, [("server", new StubSandbox())], CancellationToken.None);

        shortfall.Should().NotBeNull();
        shortfall!.Delivered.Single().PhaseId.Should().Be("p0001a");
        shortfall.NotDelivered.Single().PhaseId.Should().Be("p0001b");
    }

    [Fact]
    public async Task Prepare_ASandboxWithoutAVerifiedHead_RefusesTheDelivery()
    {
        var pipeline = Pipeline(withHead: false);

        var shortfall = await _sut.PrepareAsync(pipeline, [("server", new StubSandbox())], CancellationToken.None);

        shortfall.Should().BeNull("what the sandbox verified is unknown, so the run stays failed");
    }

    [Fact]
    public void PullRequestBanner_SaysDeliveredAndWhatStoppedTheRun()
    {
        var banner = _sut.PullRequestBanner(RunShortfall.Of(OneDoneOneOpen, "budget exhausted")!);

        banner.Should().StartWith("> ✅ **Delivered with a shortfall** — 1 of 2 phase(s)")
            .And.Contain("Not delivered").And.Contain("budget exhausted")
            .And.NotContain("Run failed").And.NotContain("do not merge");
    }

    [Fact]
    public void StepResult_NamesTheDeliveryAndThePr()
    {
        var result = _sut.StepResult(
            RunShortfall.Of(OneDoneOneOpen, "budget exhausted")!,
            [new OpenedPullRequest("primary", "https://stub.test/pulls/7", OpenStatus.Opened)]);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Delivered 1 of 2 phase(s)").And.Contain("p0001b")
            .And.Contain("https://stub.test/pulls/7").And.NotContain("Run failed");
    }

    private static PipelineContext Pipeline(bool withHead)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.FailureReason, "budget exhausted");
        pipeline.Set(ContextKeys.SpecSequenceProgress, OneDoneOneOpen);
        if (withHead)
            pipeline.Set<IReadOnlyDictionary<string, string>>(
                ContextKeys.VerifiedHeads, new Dictionary<string, string> { ["server"] = "stub-head" });
        return pipeline;
    }
}
