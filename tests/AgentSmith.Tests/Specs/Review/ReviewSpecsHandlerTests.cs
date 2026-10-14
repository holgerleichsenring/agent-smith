using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using static AgentSmith.Tests.Specs.Review.ReviewLoopRig;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>
/// 2026-10-13-8762g: the review step — what it skips and says so, what an approved text and an executed head
/// spare, the share that never stops the first reviewed spec, the stop it sets once the share is spent, and
/// a bad setting that fails the step by name.
/// </summary>
public sealed class ReviewSpecsHandlerTests
{
    private static readonly SpecReviewCut FlushCut = CutRevisionFixtures.Cut(
        SpecRegisterNames.Scope, "The writer flushes after every order", "The writer flushes once per batch");
    private static readonly (string, string) GoalOnly =
        ("every order of the day into one file", "every order of the day into one dated file");

    [Fact]
    public async Task ReviewSpecs_RoundsZero_SkipsAndSaysSo()
    {
        var rig = new ReviewLoopRig();

        var result = await rig.ExecuteAsync(Settings(rounds: 0));

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("spec review skipped: spec_review.rounds is 0");
        rig.Inputs.Should().BeEmpty();
    }

    [Fact]
    public async Task ReviewSpecs_SpecEqualToTheApprovedText_IsNotReviewed()
    {
        var rig = new ReviewLoopRig();
        Approve(rig, rig.Set);

        var result = await rig.ExecuteAsync(Settings());

        result.Message.Should().Contain("every unbuilt spec is still the text a person approved");
        rig.Inputs.Should().BeEmpty();
    }

    [Fact]
    public async Task ReviewSpecs_ApprovedSetReDerivedByRework_IsReviewed()
    {
        var rig = new ReviewLoopRig();
        var approved = rig.Set;
        Approve(rig, approved);
        var recut = approved.Specs[1] with
        {
            Draft = approved.Specs[1].Draft with { Yaml = approved.Specs[1].Draft.Yaml.Replace("one file", "one zipped file") },
        };
        rig.Pipeline.Set(ContextKeys.SpecSet, approved with { Specs = [approved.Specs[0], recut] });

        await rig.ExecuteAsync(Settings());

        rig.Inputs.Should().ContainSingle("the approval rides the re-cut set, but its text is not the approved one")
            .Which.Draft.SpecId.Should().Be(Second);
    }

    [Fact]
    public async Task ReviewSpecs_ShareSpentBeforeFirstSpec_FirstSpecStillReviewedToItsStop()
    {
        var rig = new ReviewLoopRig().Answers(Cuts(FlushCut), Build()).Revisions(GoalOnly);
        rig.Pipeline.Set(ContextKeys.RunStartedAt, DateTimeOffset.UtcNow.AddSeconds(-500));

        var result = await rig.ExecuteAsync(Settings(wallSeconds: 1000));

        rig.Inputs.Select(i => (i.Draft.SpecId, i.Round)).Should().Equal((First, 1), (First, 2));
        result.Message.Should().Contain($"{First} cleared after 2 round(s)");
        rig.Pipeline.Get<SpecSequenceStop>(ContextKeys.SpecSequenceStop).BeforeSpecId.Should().Be(Second);
    }

    [Fact]
    public async Task ReviewSpecs_FirstSpecPastTheWallTimeCeiling_StopsAsCutMidReview()
    {
        var rig = new ReviewLoopRig().Answers(Cuts(FlushCut)).Revisions(GoalOnly);
        rig.OnReview = _ => rig.Pipeline.Set(ContextKeys.RunStartedAt, DateTimeOffset.UtcNow.AddSeconds(-2000));

        var result = await rig.ExecuteAsync(Settings(wallSeconds: 1000));

        rig.Inputs.Should().ContainSingle();
        var stop = rig.Pipeline.Get<SpecSequenceStop>(ContextKeys.SpecSequenceStop);
        (stop.BeforeSpecId, stop.Park).Should().Be((First, false));
        stop.Reason.Should().Contain("wall-time ceiling (1000 s) is reached");
        result.Message.Should().Contain($"stopped before {First}");
    }

    [Fact]
    public async Task ReviewSpecs_HandedBackSet_IsNotReviewed()
    {
        var rig = new ReviewLoopRig();
        rig.Pipeline.Set(ContextKeys.SpecSet, rig.Set with
        {
            Handback = new SpecHandback(SpecHandbackCase.RequirementsContradictRepository, "the repository disagrees"),
        });

        var result = await rig.ExecuteAsync(Settings());

        result.Message.Should().Be("spec review skipped: the derivation handed the ticket back");
        rig.Inputs.Should().BeEmpty();
    }

    [Fact]
    public async Task ReviewSpecs_ExecutedHead_IsNeverReviewed_TailIs()
    {
        var rig = new ReviewLoopRig();
        rig.Pipeline.Set(ContextKeys.SpecSet, rig.Set with { Executed = [First] });

        await rig.ExecuteAsync(Settings());

        rig.Inputs.Should().ContainSingle().Which.Draft.SpecId.Should().Be(Second);
    }

    [Fact]
    public async Task ReviewSpecs_ConfigWithAProblem_FailsTheStepNamingIt()
    {
        var rig = new ReviewLoopRig();
        var settings = new SpecReviewSettings(new SpecReviewConfig { Reviewers = 0 }, 1800);

        var result = await rig.ExecuteAsync(settings);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("spec_review.reviewers is 0");
        rig.Inputs.Should().BeEmpty();
    }

    [Fact]
    public async Task ReviewSpecs_CostShareSpent_StopsAndSetsTheSequenceStopAtTheFirstUnclearedSpec()
    {
        var rig = new ReviewLoopRig();
        var tracker = PipelineCostTracker.GetOrCreate(rig.Pipeline);
        tracker.ApplyCostCap(new CostCapValues { Usd = 10m, Tokens = 10_000_000 });
        rig.OnReview = _ => Spend(tracker, 4.5m);

        var result = await rig.ExecuteAsync(Settings());

        rig.Inputs.Should().ContainSingle().Which.Draft.SpecId.Should().Be(First);
        var stop = rig.Pipeline.Get<SpecSequenceStop>(ContextKeys.SpecSequenceStop);
        stop.BeforeSpecId.Should().Be(Second);
        stop.Reason.Should().Contain("40% share of the run's cost cap");
        result.Message.Should().Contain($"stopped before {Second}");
    }

    [Fact]
    public async Task ReviewSpecs_WallClockShareSpent_StopsBeforeTheNextRound()
    {
        var rig = new ReviewLoopRig().Answers(Build(), Cuts(FlushCut)).Revisions(GoalOnly);
        rig.OnReview = input =>
        {
            if (input.Draft.SpecId == Second) rig.Pipeline.Set(ContextKeys.RunStartedAt, DateTimeOffset.UtcNow.AddSeconds(-500));
        };

        await rig.ExecuteAsync(Settings(wallSeconds: 1000));

        rig.Inputs.Select(i => (i.Draft.SpecId, i.Round)).Should().Equal((First, 1), (Second, 1));
        var stop = rig.Pipeline.Get<SpecSequenceStop>(ContextKeys.SpecSequenceStop);
        stop.BeforeSpecId.Should().Be(Second, "a spec cut mid-review is not cleared");
        stop.Reason.Should().Contain("share of the run's wall clock");
    }

    [Fact]
    public async Task ReviewPanel_Input_CarriesTicketTextSetGoalAndDescribesToday()
    {
        var rig = new ReviewLoopRig(TwoSpecs(secondRequires: First));
        rig.Pipeline.Set(ContextKeys.SpecSet, rig.Set with { Goal = "Accounting reads one file per day." });
        var ticket = new Ticket(new TicketId("42"), "Nightly export", "Write the day's orders to one file.", null, "Open", "GitHub");

        await rig.ExecuteAsync(Settings(), ticket);

        rig.Inputs.Should().HaveCount(2);
        rig.Inputs[0].WiderGoal.Should().Contain("Nightly export").And.Contain("Write the day's orders")
            .And.Contain("Accounting reads one file per day.");
        rig.Inputs.Select(i => i.DescribesToday).Should().Equal(true, false);
        rig.Inputs.Should().OnlyContain(i => i.Scope == SpecReviewScopes.Spec);
    }

    [Fact]
    public void SpecReviewBudget_Spent_HasNoFirstSpecExemption()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunStartedAt, DateTimeOffset.UtcNow.AddSeconds(-500));
        var settings = Settings(wallSeconds: 1000);

        SpecReviewBudget.Spent(pipeline, settings).Should().Contain("40% share of the run's wall clock",
            "the design turn calls Spent where no spec is first; the floor lives in the caller");
        SpecReviewBudget.Exhausted(pipeline, settings).Should().BeNull("the ceiling itself is not reached");
    }

    [Fact]
    public void CodePreset_ReviewSpecs_SitsBetweenSpecHandbackAndSpecGate()
    {
        var code = PipelinePresets.Code.ToList();

        code.IndexOf(CommandNames.ReviewSpecs).Should().Be(code.IndexOf(CommandNames.SpecHandback) + 1);
        code.IndexOf(CommandNames.SpecGate).Should().Be(code.IndexOf(CommandNames.ReviewSpecs) + 1);
        PipelinePresets.CodeSpecBlock.Should().NotContain(CommandNames.ReviewSpecs);
    }

    private static void Approve(ReviewLoopRig rig, SpecSet set) =>
        rig.Pipeline.Set(ContextKeys.ApprovedSpecSet, SpecApprovalJson.Write(new SpecApprovalRecord("shop-42",
            set with { Approval = new SpecApproval(DateTimeOffset.UtcNow, "session-1", "carol") }, ["shop"])));

    private static void Spend(PipelineCostTracker tracker, decimal usd) =>
        tracker.SeedPriorSpend(new PriorSpendSnapshot(0, 0, 0, 0, usd, 0, 0, 0, 0, 0m, string.Empty));
}
