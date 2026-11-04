using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-11-04-96a0a: the design master names a conversation that has none through name_conversation — seeded only
/// while the conversation has no subject, offered to the master and never to a child, and asked for by the prompt.
/// </summary>
public sealed class NameConversationSurfaceTests
{
    private const string DesignMaster = "design-partner-master";

    [Fact]
    public async Task NameConversation_OneLineOfProse_SetsTheSlotSubject()
    {
        var slot = new SpecDialogReplySlot();

        var answer = await Tool(slot).InvokeAsync(new AIFunctionArguments { ["subject"] = "Paging the ledger reader" });

        slot.Subject.Should().Be("Paging the ledger reader");
        answer!.ToString().Should().Be(NameConversationToolHost.Stored);
    }

    [Theory]
    [InlineData("Paging the ledger.\nAnd its export.")]
    [InlineData("**Paging the ledger reader**")]
    public async Task NameConversation_AParagraphOrMarkup_ReturnsTheRuleAndSetsNothing(string subject)
    {
        var slot = new SpecDialogReplySlot();

        var answer = await Tool(slot).InvokeAsync(new AIFunctionArguments { ["subject"] = subject });

        slot.Subject.Should().BeNull();
        answer!.ToString().Should().StartWith("Not stored: The name");
    }

    [Fact]
    public void SpecDialogTurnSeeds_AConversationWithoutSubject_SeedsNameable() =>
        Seeds(subject: null).Should().ContainKey(ContextKeys.SpecDialogNameable)
            .WhoseValue.Should().Be(true);

    [Fact]
    public void SpecDialogTurnSeeds_AConversationWithSubject_SeedsNothing() =>
        Seeds(subject: "Paging the ledger reader").Should().NotContainKey(ContextKeys.SpecDialogNameable);

    [Fact]
    public async Task MasterToolComposition_NameableDesignTurn_OffersNameConversationToTheMasterOnly()
    {
        var loop = new SurfaceCapturingLoop(spawn: true);
        var children = new MasterHandlerFixture.StubSubAgentRunner();

        await MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog(DesignMaster, "body"), null,
                0, subAgents: children, limits: new LoopLimitsConfig())
            .ExecuteAsync(DesignContext(nameable: true), CancellationToken.None);

        loop.MasterTools.Should().Contain(NameConversationToolHost.Name);
        children.SeenContexts.Single().ChildTools.Select(t => t.Name).Should()
            .NotContain(NameConversationToolHost.Name, "a child names nothing on the master's behalf");
    }

    [Fact]
    public async Task MasterToolComposition_ANamedConversation_OffersNoNameConversation()
    {
        var loop = new SurfaceCapturingLoop();

        await MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog(DesignMaster, "body"), null,
                0, limits: new LoopLimitsConfig())
            .ExecuteAsync(DesignContext(nameable: false), CancellationToken.None);

        loop.MasterTools.Should().NotContain(NameConversationToolHost.Name);
    }

    [Fact]
    public void SpecDialogPromptFactory_NameableTurn_AsksForTheNameInTheReplyLanguage()
    {
        var pipeline = Transcript();
        var unnamed = new SpecDialogPromptFactory().Build(pipeline, 0, 0);
        pipeline.Set(ContextKeys.SpecDialogNameable, true);

        var prompt = new SpecDialogPromptFactory().Build(pipeline, 0, 0);

        prompt.Should().Contain("call name_conversation once, in the language of this reply")
            .And.Contain("It is not a source read");
        unnamed.Should().NotContain("name_conversation", "a turn that is not offered the tool is not asked for it");
    }

    private static AIFunction Tool(SpecDialogReplySlot slot) =>
        new NameConversationToolHost(slot).GetTools(null, null).Single();

    private static Dictionary<string, object> Seeds(string? subject) =>
        SpecDialogTurnSeeds.Build(
            new ConversationState
            {
                JobId = "s-1", Project = "p", ChannelId = "d-1", UserId = "person-a", Subject = subject,
                Platform = "dashboard", TicketId = string.Empty, StartedAt = DateTimeOffset.UnixEpoch,
            },
            [new RepoConnection { Name = "sample-api" }],
            new Dictionary<string, Contracts.Sandbox.ISandbox>(), new SpecDialogReplySlot(),
            DialogImageSet.None, Mock.Of<IFiledTicketWithdrawal>());

    private static PipelineContext Transcript()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript,
            [new SpecDialogTurn(SpecDialogTurn.UserRole, "how does the ledger reader page?")]);
        return pipeline;
    }

    private static AgenticMasterContext DesignContext(bool nameable)
    {
        var context = MasterHandlerFixture.BuildContext(DesignMaster, includeTicket: false);
        context.Pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDialogName);
        context.Pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript,
            [new SpecDialogTurn(SpecDialogTurn.UserRole, "how does the ledger reader page?")]);
        context.Pipeline.Set(ContextKeys.SpecDialogReplySlot, new SpecDialogReplySlot());
        if (nameable) context.Pipeline.Set(ContextKeys.SpecDialogNameable, true);
        return context;
    }
}
