using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Tests.Handlers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>2026-11-12-92e9b: the design partner reads the review of its final draft before it answers; the loop ends
/// when every final draft text has a result in the turn's ledger, never on a count.</summary>
public sealed class SpecDialogTurnFlowTests
{
    private const string FirstReply = "Here is the spec.";

    [Fact]
    public async Task LoopEndsWithUnattemptedDraft_ReviewReportReachesMasterBeforeReply()
    {
        var (rig, loop) = Turn(_ => Refuse("the writer is not buffered"), _ => Result("The objection stands: buffered."));

        var reply = await ConcludeAsync(rig, loop);

        var report = loop.Requests.Should().ContainSingle().Subject;
        report.UserPrompt.Should().StartWith(DesignTurnReviewReport.Heading).And.Contain("the writer is not buffered");
        report.PriorMessages.Should().NotBeNullOrEmpty("the report continues the master's own thread");
        report.UserImageParts.Should().BeNull();
        reply.Response.Text.Should().Be("The objection stands: buffered.");
        rig.Outcome.ReviewRounds.Should().ContainSingle().Which.Verdict.Should().Be(SpecReviewVerdicts.Refuse);
        rig.Reviewed.Should().HaveCount(2, "the unchanged text is not attacked again after the report pass");
    }

    [Fact]
    public async Task LoopEndsWithAttemptedDrafts_NoFurtherPass()
    {
        var (rig, loop) = Turn(_ => Build(), _ => throw new InvalidOperationException("no pass expected"));
        await rig.Pass().ReviewAsync(new ProposalTurn(rig.Pipeline, new AgentConfig(), PipelineCostTracker.GetOrCreate(rig.Pipeline)),
            rig.Settings, Application.Services.SpecDialog.Writes.ProposalDraftOutcome.For(rig.Slot.Proposal), CancellationToken.None);

        var reply = await ConcludeAsync(rig, loop);

        loop.Requests.Should().BeEmpty();
        reply.Response.Text.Should().Be(FirstReply);
        rig.Reviewed.Should().HaveCount(2, "only the pass the loop already ran");
    }

    [Fact]
    public async Task CostCapReached_PassReviewsNothing_NoReportPass()
    {
        var (rig, loop) = Turn(_ => Refuse("never asked"), _ => throw new InvalidOperationException("no pass expected"));
        var tracker = PipelineCostTracker.GetOrCreate(rig.Pipeline);
        tracker.ApplyCostCap(new CostCapValues { Usd = 10m, Tokens = 10 });
        tracker.Track(new ChatResponse(new ChatMessage(ChatRole.Assistant, "x"))
        {
            Usage = new UsageDetails { InputTokenCount = 1_000, OutputTokenCount = 10 },
        });

        await ConcludeAsync(rig, loop);

        loop.Requests.Should().BeEmpty();
        rig.Outcome.NotReviewed.Should().Equal(Lone);
    }

    [Fact]
    public async Task ReviewedByTool_UnchangedText_OutcomeCarriesToolResult()
    {
        var (rig, loop) = Turn(_ => Refuse("the tool's reviewers say no"), _ => throw new InvalidOperationException("no pass"));
        var tool = new ReviewProposalToolHost(rig.Pass(), Reader(() => rig.Settings), rig.Pipeline, new AgentConfig());
        await tool.GetTools(null, null).Single().InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        await ConcludeAsync(rig, loop);

        loop.Requests.Should().BeEmpty();
        rig.Outcome.ReviewRounds.Should().ContainSingle().Which.Findings.Should().Contain(f => f.Text == "the tool's reviewers say no");
        rig.Reviewed.Should().HaveCount(2, "the tool's pass is the only one");
    }

    [Fact]
    public async Task ExtraPassThrows_KeepsPreviousReply()
    {
        ProposalReviewRig? captured = null;
        var (rig, loop) = Turn(_ => Refuse("the writer is not buffered"), _ =>
        {
            captured!.Slot.Proposal.Seed(Application.Services.SpecDialog.Writes.ProposalDraftSeed.Of(new AnswerOutcome()), true);
            throw new InvalidOperationException("model unreachable");
        });
        captured = rig;
        var before = rig.Slot.Proposal.Current;

        var reply = await ConcludeAsync(rig, loop);

        reply.Response.Text.Should().Be(FirstReply);
        rig.Slot.Proposal.Current.Should().BeSameAs(before, "the failed pass's writes are not kept");
        rig.Outcome.Should().BeOfType<SpecOutcome>().Which.ReviewRounds.Should().ContainSingle();
    }

    [Fact]
    public async Task CutsApplied_NoSecondPass_ReplyGoesOut()
    {
        var edited = new ProposalReviewRig().Propose(SpecReply(Yaml(Lone, step: "Write the orders through the export queue.")));
        ProposalReviewRig? captured = null;
        var (rig, loop) = Turn(_ => Cuts(Cut(SpecRegisterNames.Steps, "buffered file writer", "export queue")), _ =>
        {
            captured!.Slot.Proposal.Seed(Application.Services.SpecDialog.Writes.ProposalDraftSeed.Of(edited), true);
            return Result("Cuts applied.");
        });
        captured = rig;

        var reply = await ConcludeAsync(rig, loop);

        loop.Requests.Should().ContainSingle("the report of the one pass; the applied cuts are not reviewed again");
        reply.Response.Text.Should().Be("Cuts applied.");
        rig.Reviewed.Should().HaveCount(2, "one pass");
        rig.Outcome.Should().BeOfType<SpecOutcome>().Which.Draft.Yaml.Should().Contain("export queue");
        rig.Outcome.ReviewRounds.Should().ContainSingle().Which.Verdict.Should().Be(SpecReviewVerdicts.BuildWithCuts);
    }

    [Fact]
    public async Task RefusedRewritten_ReviewedAgain()
    {
        var rewritten = new ProposalReviewRig().Propose(SpecReply(Yaml(Lone, step: "Write the orders through the export queue.")));
        ProposalReviewRig? captured = null;
        var (rig, loop) = Turn(input => input.Draft.Yaml.Contains("buffered") ? Refuse("not buffered") : Build(), _ =>
        {
            captured!.Slot.Proposal.Seed(Application.Services.SpecDialog.Writes.ProposalDraftSeed.Of(rewritten), true);
            return Result("Rewritten.");
        });
        captured = rig;

        await ConcludeAsync(rig, loop);

        rig.Reviewed.Should().HaveCount(4, "the rewritten text of a refused draft gets a pass of its own");
        loop.Requests.Should().HaveCount(2);
        loop.Requests[1].UserPrompt.Should().Contain($"{Lone}: {SpecReviewVerdicts.Build}").And.NotContain(SpecReviewVerdicts.Refuse);
    }

    [Fact]
    public async Task DesignTurnReviewOff_NoExtraPass()
    {
        var (rig, loop) = Turn(_ => Refuse("never asked"), _ => throw new InvalidOperationException("no pass expected"));
        rig.Settings = Of(rounds: 4, designTurn: false);

        await ConcludeAsync(rig, loop);

        loop.Requests.Should().BeEmpty();
        rig.Reviewed.Should().BeEmpty();
        rig.Outcome.Should().BeOfType<SpecOutcome>().Which.ReviewRounds.Should().BeEmpty();
    }

    private static (ProposalReviewRig Rig, ScriptedLoop Loop) Turn(
        Func<SpecAttackInput, SpecAttackResult?> review, Func<AgenticLoopRequest, AgenticLoopResult> pass)
    {
        var rig = new ProposalReviewRig { OnReview = review }.WithRepository();
        var proposal = rig.Propose(SpecReply(Yaml(Lone)));
        rig.Pipeline.Remove(ContextKeys.SpecDialogOutcome);
        rig.Slot.Proposal.Seed(Application.Services.SpecDialog.Writes.ProposalDraftSeed.Of(proposal), true);
        return (rig, new ScriptedLoop(pass));
    }

    private static async Task<AgenticLoopResult> ConcludeAsync(ProposalReviewRig rig, ScriptedLoop loop)
    {
        var flow = new SpecDialogTurnFlow(new SpecDialogPromptFactory(), new TicketStewardPromptFactory(),
            new DesignTurnReviewDrive(rig.Review(), loop, NullLogger<DesignTurnReviewDrive>.Instance));
        var context = MasterHandlerFixture.BuildContext("design-partner-master") with { Pipeline = rig.Pipeline };
        var request = new AgenticLoopRequest(new AgentConfig(), TaskType.Primary, "system", "user", []);
        var conversation = new MasterConversation();
        conversation.Opened(request, Result(FirstReply).Response);
        return await flow.ConcludeAsync(context, request, "user", Result(FirstReply), conversation,
            PipelineCostTracker.GetOrCreate(rig.Pipeline), CancellationToken.None);
    }

    private sealed class ScriptedLoop(Func<AgenticLoopRequest, AgenticLoopResult> pass) : IAgenticLoopRunner
    {
        public List<AgenticLoopRequest> Requests { get; } = [];

        public Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(pass(request));
        }
    }
}
