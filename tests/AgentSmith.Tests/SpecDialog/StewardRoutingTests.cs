using AgentSmith.Application.Models;
using AgentSmith.Application.Prompts;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-14-da5ef: a conversation bound to a ticket a code attempt worked is led by the ticket steward; an unbound
/// or never-worked one, and any turn whose loaded catalog has no steward, keeps the design partner.
/// </summary>
public sealed class StewardRoutingTests
{
    private const string DesignPartner = "design-partner-master";

    [Fact]
    public void SpecDialogTurn_BoundWorkedTicket_UsesTheSteward()
    {
        var master = MasterFor(Bound(worked: true), Catalog(withSteward: true));

        master.MasterSkillName.Should().Be(PipelinePresets.TicketStewardMaster);
        master.IsTicketSteward.Should().BeTrue();
    }

    [Fact]
    public void SpecDialogTurn_BoundNeverWorkedTicket_UsesTheDesignPartner() =>
        MasterFor(Bound(worked: false), Catalog(withSteward: true)).MasterSkillName.Should().Be(DesignPartner);

    [Fact]
    public void SpecDialogTurn_Unbound_UsesTheDesignPartner() =>
        MasterFor(bound: null, Catalog(withSteward: true)).MasterSkillName.Should().Be(DesignPartner);

    [Fact]
    public void SpecDialogTurn_CatalogWithoutSteward_FallsBackToTheDesignPartner()
    {
        var master = MasterFor(Bound(worked: true), Catalog(withSteward: false));

        master.MasterSkillName.Should().Be(DesignPartner, "a pinned catalog without the steward must not fail the turn");
        master.IsTicketSteward.Should().BeFalse();
    }

    [Fact]
    public void SpecDialogTurn_CatalogNotLoaded_FallsBackToTheDesignPartner() =>
        MasterFor(Bound(worked: true), Catalog(withSteward: true, ready: false)).MasterSkillName.Should().Be(DesignPartner);

    [Fact]
    public void TurnSeeds_WorkedTicket_AskForTheSteward_NeverWorkedDoesNot()
    {
        Seeds(Bound(worked: true)).Should().ContainKey(ContextKeys.SpecDialogSteward);
        Seeds(Bound(worked: false)).Should().NotContainKey(ContextKeys.SpecDialogSteward);
    }

    [Fact]
    public void BoundTicket_Worked_IsTheSeriesPortPredicate()
    {
        Bound(worked: true)!.Worked.Should().BeTrue();
        Bound(worked: false)!.Worked.Should().BeFalse();
    }

    private static AgenticMasterContext MasterFor(BoundTicket? bound, IMasterCatalog catalog)
    {
        var pipeline = new PipelineContext();
        foreach (var (key, value) in Seeds(bound)) pipeline.Set(key, value);
        pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDialogName);
        pipeline.Set(ContextKeys.ResolvedPipeline,
            new ResolvedPipelineConfig(PipelinePresets.SpecDialogName, new AgentConfig(), "skills", null));
        return (AgenticMasterContext)new AgenticMasterContextBuilder(catalog).Build(
            PipelineCommand.Simple(CommandNames.AgenticMaster), new ResolvedProject(), pipeline);
    }

    private static Dictionary<string, object> Seeds(BoundTicket? bound)
    {
        var at = DateTimeOffset.UnixEpoch;
        var state = new ConversationState
        {
            JobId = "s-1", Project = "p", ChannelId = "d-1", UserId = "person-a",
            Platform = "dashboard", TicketId = string.Empty, StartedAt = at,
            Transcript = [new TranscriptTurn(TranscriptRole.User, "why did attempt 2 stop?", at)],
        };
        return SpecDialogTurnSeeds.Build(
            state, [new RepoConnection { Name = "sample-api" }], new Dictionary<string, ISandbox>(),
            new SpecDialogReplySlot(), DialogImageSet.None, Mock.Of<IFiledTicketWithdrawal>(), bound: bound);
    }

    private static BoundTicket? Bound(bool worked) => new(
        Mock.Of<ITicketReader>(), Mock.Of<IBoundTicketWork>(), Mock.Of<IBoundTicketRuns>(),
        new FrameworkFacts("agentsmith/1", "enqueued", "in-progress", "approved"),
        Series: worked ? new BoundSeries(Mock.Of<ISeriesRead>(), null) : null);

    /// <summary>The real catalog over a loaded skill set holding the design partner and, when asked, a stub
    /// master named ticket-steward-master.</summary>
    private static SkillCatalogPromptCatalog Catalog(bool withSteward, bool ready = true)
    {
        List<RoleSkillDefinition> masters = [Master(DesignPartner)];
        if (withSteward) masters.Add(Master(PipelinePresets.TicketStewardMaster));
        var loader = new Mock<ISkillLoader>();
        loader.Setup(l => l.LoadRoleDefinitions(It.IsAny<string>())).Returns(masters);
        var path = new Mock<ISkillsCatalogPath>();
        if (ready) path.Setup(p => p.Root).Returns("/catalog");
        else path.Setup(p => p.Root).Throws(new InvalidOperationException("Catalog not ready"));
        return new SkillCatalogPromptCatalog(Mock.Of<IPromptCatalog>(), loader.Object, path.Object,
            Mock.Of<ISkillBodyResolver>(), NullLogger<SkillCatalogPromptCatalog>.Instance);
    }

    private static RoleSkillDefinition Master(string name) => new()
    {
        Name = name, Role = "master", Description = "stub master", Rules = "body",
    };
}
