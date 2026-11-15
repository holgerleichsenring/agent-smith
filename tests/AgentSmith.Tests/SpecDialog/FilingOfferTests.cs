using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-11-15-bf77: offer_filing binds the master's line to the draft it was made on; a write after it withdraws the
/// offer, and the outcome, the stored row and the push carry it beside whether the master leads filing at all.
/// </summary>
public sealed class FilingOfferTests
{
    private static readonly SpecDraft Draft = new("s-1", "Export the report as CSV.", "spec: s-1", []);

    [Fact]
    public async Task OfferFiling_OnADraft_BindsTheLineAndRepublishesTheDraft()
    {
        var slot = Drafted(changed: false);

        var answer = await OfferAsync(slot, "  Files one ticket for the export.  ");

        answer.Should().StartWith("Offered");
        slot.OfferedLine.Should().Be("Files one ticket for the export.");
        slot.Proposal.Changed.Should().BeTrue("the offer rides a proposal, so the unchanged draft is published again");
    }

    [Fact]
    public async Task OfferFiling_EmptyDraft_IsRefused()
    {
        var slot = new SpecDialogReplySlot();

        (await OfferAsync(slot, "Files it.")).Should().StartWith("Not offered").And.Contain("empty");
        slot.Offer.Should().BeNull();
    }

    [Fact]
    public async Task OfferFiling_EmptyLine_IsRefused()
    {
        var slot = Drafted(changed: true);

        (await OfferAsync(slot, " ")).Should().StartWith("Not offered").And.Contain("line");
        slot.Offer.Should().BeNull();
    }

    [Fact]
    public async Task OfferedLine_AWriteAfterTheOffer_WithdrawsIt()
    {
        var slot = Drafted(changed: true);
        await OfferAsync(slot, "Files one ticket.");

        slot.Proposal.Apply(current => new DraftWrite(current with { Comment = null }, null));

        slot.OfferedLine.Should().BeNull("the offer named the draft before the write");
    }

    [Fact]
    public void DescribedIn_NamesTheToolOnlyWhenTheSkillDoes()
    {
        OfferFilingToolHost.DescribedIn("Call offer_filing when the draft is ready.").Should().BeTrue();
        OfferFilingToolHost.DescribedIn("Draft the specs.").Should().BeFalse();
        OfferFilingToolHost.DescribedIn(null).Should().BeFalse();
    }

    [Theory]
    [InlineData("Offer the draft with offer_filing.", true)]
    [InlineData("Draft the specs.", false)]
    public void TicketActTools_OfferLed_FollowsWhetherTheMasterSkillNamesTheTool(string skill, bool led)
    {
        var catalog = new Mock<IPromptCatalog>();
        catalog.Setup(c => c.Get("design-partner-master")).Returns(skill);
        var slot = new SpecDialogReplySlot();
        var context = new AgenticMasterContext("design-partner-master",
            new Repository(new BranchName("main"), "/tmp"), string.Empty, new AgentConfig { Type = "claude" }, new PipelineContext());
        context.Pipeline.Set(ContextKeys.SpecDialogReplySlot, slot);

        var tools = new TicketActToolsFactory(Handlers.MasterHandlerFixture.Verification(),
            Handlers.MasterHandlerFixture.ProposalWrites(), prompts: catalog.Object).Tools(context);

        tools.Select(t => t.Name).Should().Contain(OfferFilingToolHost.Name);
        slot.OfferLed.Should().Be(led);
    }

    [Fact]
    public void TicketStewardTools_ExcludeOfferFiling()
    {
        IList<AITool> master = [.. new OfferFilingToolHost(new SpecDialogReplySlot()).GetTools(null, null)];

        TicketStewardTools.Of(master).Should().BeEmpty();
    }

    [Fact]
    public async Task CollectSpecDialogReply_StampsOfferLedAndTheLineOfTheCurrentDraft()
    {
        var slot = Drafted(changed: true);
        slot.OfferLed = true;
        await OfferAsync(slot, "Files one ticket.");
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecDialogReplySlot, slot);
        pipeline.Set(ContextKeys.MasterAnswer, "Ready to file.");
        pipeline.Set(ContextKeys.SpecDialogOutcome, (OutcomeProposal)new SpecOutcome(Draft));

        await new CollectSpecDialogReplyHandler().ExecuteAsync(new CollectSpecDialogReplyContext(pipeline), CancellationToken.None);

        slot.Outcome!.OfferLed.Should().BeTrue();
        slot.Outcome.FilingOffer.Should().Be("Files one ticket.");
    }

    [Fact]
    public void OutcomeProposalJson_RoundTripsTheOffer()
    {
        var proposal = new SpecOutcome(Draft) { OfferLed = true, FilingOffer = "Files one ticket." };

        var back = OutcomeProposalJson.Read(OutcomeProposalJson.Write(proposal));

        back.OfferLed.Should().BeTrue();
        back.FilingOffer.Should().Be("Files one ticket.");
        OutcomeProposalJson.Read(OutcomeProposalJson.Write(new SpecOutcome(Draft))).OfferLed.Should().BeFalse();
    }

    [Fact]
    public void ProposalComposer_CarriesTheOfferOntoThePush()
    {
        var push = new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()).Compose(
            "d-1", new SpecOutcome(Draft) { OfferLed = true, FilingOffer = "Files one ticket." }, DateTimeOffset.UnixEpoch)!;

        push.OfferLed.Should().BeTrue();
        push.FilingOffer.Should().Be("Files one ticket.");
    }

    private static SpecDialogReplySlot Drafted(bool changed)
    {
        var slot = new SpecDialogReplySlot();
        slot.Proposal.Seed(new ProposalDraft { Specs = [new DraftSpec(Draft, Parent: false)] }, changed);
        return slot;
    }

    private static async Task<string> OfferAsync(SpecDialogReplySlot slot, string line) =>
        (await new OfferFilingToolHost(slot).GetTools(null, null).Single().InvokeAsync(
            new AIFunctionArguments { ["line"] = line }))!.ToString()!;
}
