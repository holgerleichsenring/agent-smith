using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.AI;
using AgentSmith.Tests.TestSupport;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-10-13-8762i: the review share bounds a design turn, the first open draft is reviewed whatever the share,
/// and a later turn carries over what an earlier one cleared — so a large epic is reviewed across turns.
/// </summary>
public sealed class ProposalReviewTurnsTests
{
    private const string Step = "Write the orders through the buffered file writer.";
    private const string NewStep = "Write the orders through the streaming file writer.";

    [Fact]
    public async Task ProposalReview_ShareSpentBeforeReview_FirstOpenDraftStillReviewed()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 4, wall: 1000) };
        rig.Pipeline.Set(ContextKeys.RunStartedAt, DateTimeOffset.UtcNow.AddSeconds(-500));
        rig.Propose(Epic());

        await rig.RunAsync();

        rig.Reviewed.Select(i => i.Draft.SpecId).Distinct().Should().Equal(First);
        rig.Outcome.NotReviewed.Should().Equal(Second);
        rig.Outcome.Findings.Should().Contain(f => f.SpecId == Second && f.Problem == "not reviewed yet");
    }

    [Fact]
    public async Task ProposalReview_TimeShareSpent_NoFurtherRound()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 4, wall: 1000) };
        rig.Propose(Epic());
        rig.OnReview = input => input.Round == 1 ? RefuseCuts(Cut(SpecRegisterNames.Steps, Step, NewStep)) : Build();
        rig.OnReprompt = () => rig.Pipeline.Set(ContextKeys.RunStartedAt, DateTimeOffset.UtcNow.AddSeconds(-500));
        rig.Replies(_ => Epic(Yaml(First, step: NewStep), Yaml(Second, step: NewStep, requires: [First])));

        await rig.RunAsync();

        rig.Reviewed.Count(i => i.Draft.SpecId == Second).Should().Be(2, "one round, then the share was spent");
        rig.Outcome.NotReviewed.Should().Equal(Second);
        rig.Reviewed.Count(i => i.Draft.SpecId == First).Should().Be(4, "the floor draft is reviewed to its stop");
    }

    [Fact]
    public async Task ProposalReview_BudgetExhausted_StopsOnLastValidProposal_ListsNotReviewed()
    {
        var rig = new ProposalReviewRig();
        rig.Propose(Epic());
        rig.OnReview = input => input.Round == 1 ? RefuseCuts(Cut(SpecRegisterNames.Steps, Step, NewStep)) : Build();
        rig.OnReprompt = () =>
        {
            var tracker = PipelineCostTracker.GetOrCreate(rig.Pipeline);
            tracker.ApplyCostCap(new CostCapValues { Usd = 10m, Tokens = 10 });
            tracker.Track(new ChatResponse(new ChatMessage(ChatRole.Assistant, "x"))
            {
                Usage = new UsageDetails { InputTokenCount = 1_000, OutputTokenCount = 10 },
            });
        };
        rig.Replies(_ => Epic(Yaml(First, step: NewStep), Yaml(Second, step: NewStep, requires: [First])));

        await rig.RunAsync();

        rig.DraftOf(First).Yaml.Should().Contain(NewStep, "the last valid proposal is kept");
        rig.Outcome.NotReviewed.Should().Equal(First, Second);
        rig.Reviewed.Should().HaveCount(4, "no round starts once the run cap is spent");
    }

    [Fact]
    public async Task ProposalReview_FloorDraft_StopsAsNotReviewedBeforeTheWallClockCeiling()
    {
        var clock = new SettableClock();
        var rig = new ProposalReviewRig { Settings = Of(rounds: 4, wall: 1800), Clock = clock };
        rig.Pipeline.Set(ContextKeys.RunStartedAt, clock.GetUtcNow().AddSeconds(-1500));
        rig.Propose(SpecReply(Yaml(Lone)));
        rig.OnReview = _ => RefuseCuts(Cut(SpecRegisterNames.Steps, Step, NewStep));
        rig.OnReprompt = () => clock.Now = clock.Now.AddSeconds(200);
        rig.Replies(_ => SpecReply(Yaml(Lone, step: NewStep)));

        await rig.RunAsync();

        rig.Outcome.ReviewRounds.Should().ContainSingle("a second round of 200 s would pass the 1800 s ceiling");
        rig.Outcome.NotReviewed.Should().Equal(Lone);
    }

    [Fact]
    public async Task ProposalReview_EditTurnAfterNotReviewed_ReviewsOnlyTheUnreviewedDrafts()
    {
        var rig = new ProposalReviewRig();
        var earlier = rig.Propose(Epic());
        rig.Pipeline.Set(ContextKeys.SpecDialogRevisedProposal, earlier with
        {
            ReviewRounds = [Round(First)],
            NotReviewed = [Second],
        });

        await rig.RunAsync();

        rig.Reviewed.Select(i => i.Draft.SpecId).Distinct().Should().Equal(Second);
        rig.Outcome.ReviewRounds.Count(r => r.SpecId == First).Should().Be(1, "its earlier round is carried");
        rig.Outcome.NotReviewed.Should().BeNull();
    }

    [Fact]
    public async Task ProposalReview_EpicWithContract_SeriesRoundIsNotAParentRound()
    {
        var rig = new ProposalReviewRig();
        rig.Propose(Epic());

        await rig.RunAsync();

        rig.Outcome.ReviewRounds.Select(r => r.SpecId).Should().OnlyContain(id => id == First || id == Second,
            "the parent is never keyed by a round; the series round has its own token");
    }

    [Fact]
    public async Task ProposalReview_UnchangedParentCarriedOver_SeriesRoundNotDuplicated()
    {
        var rig = new ProposalReviewRig();
        var earlier = rig.Propose(Epic());
        rig.Pipeline.Set(ContextKeys.SpecDialogRevisedProposal, earlier with
        {
            ReviewRounds = [Round(First), Round(Second), Round(ProposalReviewTokens.Series)],
        });

        await rig.RunAsync();

        rig.Reviewed.Should().BeEmpty("both children are carried, unchanged");
        rig.Outcome.ReviewRounds.Select(r => r.SpecId).Should().Equal(First, Second);
    }

    [Fact]
    public async Task ProposalReview_StoreEditOfRounds_AppliesToTheNextTurn()
    {
        var current = Of(rounds: 1);
        var reader = Reader(() => current);
        var first = new ProposalReviewRig();
        first.Propose(SpecReply(Yaml(Lone)));
        await first.RunAsync(reader);

        current = Of(rounds: 0);
        var second = new ProposalReviewRig();
        second.Propose(SpecReply(Yaml(Lone)));
        await second.RunAsync(reader);

        first.Reviewed.Should().HaveCount(2);
        second.Reviewed.Should().BeEmpty("the edit is read on the next turn, without a restart");
    }

    [Fact]
    public async Task ProposalReview_TurnWithTemplate_MissingCitedFileIsStillAProblem()
    {
        var template = $"{TemplateScopeName.Prefix}clean";
        var rig = new ProposalReviewRig { RealEvidence = true, Settings = Of(rounds: 1) };
        rig.Pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)new Dictionary<string, ISandbox>
        {
            ["shop"] = new EvidenceTreeSandbox(new Dictionary<string, string>()),
            [template] = new EvidenceTreeSandbox(new Dictionary<string, string>
            {
                ["src/Export/ExportHandler.cs"] = string.Join('\n', Enumerable.Repeat("x", 20)),
            }),
        });
        rig.Pipeline.Set(ContextKeys.SandboxRepos,
            (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["shop"] = "shop", [template] = template });
        rig.Propose(SpecReply(Yaml(Lone)));

        await rig.RunAsync();

        rig.Outcome.Findings.Should().ContainSingle(f => f.Problem == ProposalEvidenceReview.Problem,
                "the cited file exists only in the template, never in the repository")
            .Which.SpecId.Should().Be(Lone);
    }

    private static ProposalReviewRound Round(string specId) =>
        new(specId, 1, SpecReviewVerdicts.Build, [SpecReviewVerdicts.Build], [], [], []);
}
