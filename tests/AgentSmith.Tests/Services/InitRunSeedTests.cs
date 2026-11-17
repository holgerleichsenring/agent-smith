using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>2026-11-17-713ea: a run is an init because of the pipeline it runs, whichever door started it.</summary>
public sealed class InitRunSeedTests
{
    [Theory]
    [InlineData("init-project")]
    [InlineData("Init-Project")]
    public void InitRunSeed_TheInitPipeline_GetsInitModeAndTheInitBranch(string name)
    {
        var pipeline = new PipelineContext();

        InitRunSeed.Apply(pipeline, name);

        InitRunSeed.IsInitRun(pipeline).Should().BeTrue();
        pipeline.Get<string>(ContextKeys.CheckoutBranch).Should().Be(TicketBranchNamer.InitBranch);
    }

    [Fact]
    public void InitRunSeed_ALabelStartedInitWithATicket_IsStillAnInitOnTheInitBranch()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("42"));

        InitRunSeed.Apply(pipeline, PipelinePresets.InitProjectName);

        InitRunSeed.IsInitRun(pipeline).Should().BeTrue();
        RunBranchResolver.Resolve(pipeline)!.Name.Value.Should().Be("agent-smith/init",
            "the ticket's own branch name is not where an init writes");
    }

    [Fact]
    public void InitRunSeed_AnyOtherPipeline_IsLeftAlone()
    {
        var pipeline = new PipelineContext();

        InitRunSeed.Apply(pipeline, PipelinePresets.CodeName);

        InitRunSeed.IsInitRun(pipeline).Should().BeFalse();
        pipeline.Has(ContextKeys.CheckoutBranch).Should().BeFalse();
    }
}
