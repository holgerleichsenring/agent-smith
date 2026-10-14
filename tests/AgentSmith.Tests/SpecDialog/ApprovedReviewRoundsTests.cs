using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-13-8762l: the design conversation's review rounds move onto the approval record at filing, re-keyed
/// with the drafts' own map; the record keeps old rounds only for ids a continuation kept, and never carries
/// them into a run.
/// </summary>
public sealed class ApprovedReviewRoundsTests
{
    private const string Base = "2026-10-14-0d0d";
    private const string A = Base + "a", B = Base + "b";

    [Fact]
    public void FiledSeriesFactory_Mint_ReviewRoundsFollowTheReid()
    {
        var spec = new SpecOutcome(Draft("p-1")) { ReviewRounds = [Round("p-1", 1), Round("p-1", 2)] };

        var filed = ApprovedSetDoubles.SeriesFiling().Under(Base, [spec.Draft], spec);

        filed.ReviewRounds.Select(r => (r.SpecId, r.Round)).Should().Equal((A, 1), (A, 2));
        ApprovedSetDoubles.SeriesFiling().Mint([spec.Draft], spec).ReviewRounds.Should().ContainSingle(r => r.Round == 1)
            .Which.SpecId.Should().EndWith("a", "a minted series names the lone spec by its first member id");
    }

    [Fact]
    public void FiledSeriesFactory_Mint_SeriesRoundKept_ParentRoundsAndOtherUnmappedDropped()
    {
        var epic = new EpicOutcome(Draft("parent"), [Draft("c-1"), Draft("c-2")])
        {
            ReviewRounds = [Round("parent", 1), Round("c-1", 1), Round("c-2", 1), Round("left-out", 1), Round(ProposalReviewTokens.Series, 1)],
        };

        var filed = ApprovedSetDoubles.SeriesFiling().Under(Base, epic.Children, epic);

        filed.ReviewRounds.Select(r => r.SpecId).Should().Equal(A, B, ProposalReviewTokens.Series);
    }

    [Fact]
    public void FiledSeriesFactory_SeriesRoundCuts_AreReKeyed()
    {
        var series = Round(ProposalReviewTokens.Series, 1) with
        {
            Cuts = [Cut("c-2"), Cut("c-1"), Cut(null)],
        };
        var epic = new EpicOutcome(Draft("parent"), [Draft("c-1"), Draft("c-2")]) { ReviewRounds = [series] };

        var filed = ApprovedSetDoubles.SeriesFiling().Under(Base, epic.Children, epic);

        filed.ReviewRounds.Single().Cuts.Select(c => c.SpecId).Should().Equal(B, A, null);
    }

    [Fact]
    public void ApprovedReviewRounds_Merge_NewReplaceOldKeepsExecutedHead()
    {
        var previous = Record(Round(A, 1), Round(A, 2), Round(B, 1));
        var series = new FiledSeries(Base, [Draft(B)]) { ReviewRounds = [Round(B, 3)] };
        var continuation = new SeriesContinuation(
            [new SpecDisposition(A, SpecDispositionKinds.Kept), new SpecDisposition(B, SpecDispositionKinds.Recut)], "r-1");

        var merged = ApprovedReviewRounds.Merge(previous, series, continuation)!;

        merged.Select(r => (r.SpecId, r.Round)).Should().Equal((A, 1), (A, 2), (B, 3));
    }

    [Fact]
    public void ApprovedReviewRounds_Merge_DroppedOrReusedIdLosesOldRounds()
    {
        var previous = Record(Round(A, 1), Round(B, 1));
        var dropped = new SeriesContinuation([new SpecDisposition(B, SpecDispositionKinds.Dropped)], "r-1");

        ApprovedReviewRounds.Merge(previous, new FiledSeries(Base, [Draft(A)]), dropped).Should().BeNull();
        ApprovedReviewRounds.Merge(previous, new FiledSeries(Base, [Draft(A), Draft(B)]), null)
            .Should().BeNull("a filing keeps nothing old, so a reused id never inherits a dropped spec's rounds");
    }

    [Fact]
    public void SpecApprovalJson_ReviewRounds_RoundTrip_AndAnOldRecordReadsNone()
    {
        var record = Record(Round(A, 1) with { Cuts = [Cut(B)], ReviewerVerdicts = ["refuse", null] });

        var back = SpecApprovalJson.Read(SpecApprovalJson.Write(record))!;

        back.ReviewRounds!.Single().Should().BeEquivalentTo(record.ReviewRounds!.Single());
        var old = System.Text.Json.Nodes.JsonNode.Parse(SpecApprovalJson.Write(record))!.AsObject();
        old.Remove("reviewRounds").Should().BeTrue();
        SpecApprovalJson.Read(old.ToJsonString())!.ReviewRounds.Should().BeNull("a record written before 8762l has none");
    }

    [Fact]
    public async Task ApprovedSpecSetCarrier_RecordWithRounds_CarriesNone()
    {
        var store = ApprovedSetDoubles.Store();
        await store.SaveAsync(Record(Round(A, 1)), default);

        var json = await ApprovedSetDoubles.Carrier(store).JsonForAsync(ApprovedSets.Tracker, "github", "42", default);

        SpecApprovalJson.Read(json)!.ReviewRounds.Should().BeNull();
        (await store.GetAsync(ApprovedSets.Tracker, "github-42", default))!.ReviewRounds.Should().ContainSingle("the store keeps them");
    }

    [Fact]
    public async Task ApprovedSpecSetResolver_PublishedRecord_CarriesNoRounds()
    {
        var store = ApprovedSetDoubles.Store();
        await store.SaveAsync(Record(Round(A, 1)), default);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TrackerConnection, ApprovedSets.Tracker);

        await ApprovedSetDoubles.Resolver(store).ResolveAsync(pipeline, new TicketKey("github-42"), default);

        SpecApprovalJson.Read(pipeline.Get<string>(ContextKeys.ApprovedSpecSet))!.ReviewRounds.Should().BeNull();
    }

    [Fact]
    public void SpecCutContent_ReviewObjectionQuestion_CaseIsReviewObjection()
    {
        var question = new SpecHandback(SpecHandbackCase.Question, "a reason");

        SpecCutContent.CaseOf(question with { QuestionKind = SpecQuestionKind.ReviewObjection }).Should().Be("ReviewObjection");
        SpecCutContent.CaseOf(question).Should().Be("Question");
        SpecCutContent.CaseOf(new SpecHandback(SpecHandbackCase.Refused, "no") { QuestionKind = SpecQuestionKind.ReviewObjection })
            .Should().Be("Refused", "only a question hand-back carries a question kind");
    }

    internal static ProposalReviewRound Round(string specId, int round) =>
        new(specId, round, SpecReviewVerdicts.BuildWithCuts, ["build-with-cuts", "build"], [Cut(null)],
            [new SpecReviewFinding("A finding", "shop/src/A.cs:3 [A1]")], [new SpecReviewResidue("steps", "old words")]);

    internal static SpecReviewCut Cut(string? member) =>
        new("goal", "the old goal", "the new goal", "shop search:'Widget' [A2]", member);

    private static SpecApprovalRecord Record(params ProposalReviewRound[] rounds) =>
        ApprovedSets.Record("github-42", ApprovedSets.Noon) with { ReviewRounds = rounds };

    private static SpecDraft Draft(string id) => new(id, $"Goal of {id}", $"spec: {id}\ngoal: \"Goal of {id}\"\n", []) { Repos = ["api"] };
}
