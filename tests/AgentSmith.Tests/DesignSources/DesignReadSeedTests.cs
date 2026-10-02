using AgentSmith.Application.Services.Resume;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.DesignSources;

/// <summary>
/// 2026-10-01-7f7ab: where design_read's sources come from — a run's resolved project, a design
/// turn's seed — and that the seed is live state a checkpoint never carries.
/// </summary>
public sealed class DesignReadSeedTests
{
    private static readonly DesignSource Figma = new("brand", DesignSourceVendor.Figma, "figma-token");
    private readonly DesignReadToolHostFactory _factory = new(Mock.Of<IFigmaClient>(MockBehavior.Strict),
        DesignImageFakes.NoLoopDeposit(),
        new AgentSmith.Application.Services.Events.NoOpEventPublisher(),
        Microsoft.Extensions.Logging.Abstractions.NullLogger<DesignReadToolHostFactory>.Instance);

    [Fact]
    public void Factory_RunWithFigmaSource_BuildsTheHost()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject { Name = "p", DesignSources = [Figma] });

        _factory.Create(pipeline).Should().NotBeNull();
    }

    [Fact]
    public void Factory_DesignTurnSeed_BuildsTheHost()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<DesignSource>>(ContextKeys.SpecDialogDesignSources, [Figma]);

        _factory.Create(pipeline).Should().NotBeNull();
    }

    [Fact]
    public void Factory_NoSource_BuildsNothing()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject { Name = "p" });

        _factory.Create(pipeline).Should().BeNull();
        _factory.Create(new PipelineContext()).Should().BeNull();
    }

    [Fact]
    public void PipelineContextSerializer_DesignSourcesSeed_IsNotCheckpointed()
    {
        var sut = new PipelineContextSerializer(NullLogger<PipelineContextSerializer>.Instance);
        var source = new PipelineContext();
        source.Set(ContextKeys.RunId, "run-1");
        source.Set<IReadOnlyList<DesignSource>>(ContextKeys.SpecDialogDesignSources, [Figma]);

        var target = new PipelineContext();
        sut.Restore(sut.Serialize(source), target);

        target.Has(ContextKeys.SpecDialogDesignSources).Should().BeFalse("re-derived from the project on every turn");
        target.Get<string>(ContextKeys.RunId).Should().Be("run-1");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TurnSeeds_SeedTheProjectsDesignSources_OnlyWhenItHasOne(bool hasSource)
    {
        var seeds = SpecDialogTurnSeeds.Build(
            new ConversationState
            {
                JobId = "s-1", Project = "p", ChannelId = "d-1", UserId = "person-a",
                Platform = "dashboard", TicketId = string.Empty, StartedAt = DateTimeOffset.UnixEpoch,
            },
            [new RepoConnection { Name = "sample-api" }],
            new Dictionary<string, Contracts.Sandbox.ISandbox>(), new SpecDialogReplySlot(),
            DialogImageSet.None, Mock.Of<Contracts.Dialogue.IFiledTicketWithdrawal>(),
            designSources: hasSource ? [Figma] : []);

        seeds.ContainsKey(ContextKeys.SpecDialogDesignSources).Should().Be(hasSource);
    }
}
