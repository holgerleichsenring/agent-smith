using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.SpecDialog;
using AgentSmith.Tests.SpecDialog.Continuation;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>
/// 2026-10-13-8762l: GET /api/tickets/{project}/{ticketId}/reviews — every attempt's review rounds and the
/// approved set's conversation rounds, over the real registration and a migrated store.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class TicketReviewsReadTests : IDisposable
{
    private const string R1 = "2026-10-13T09-00-00-0001";
    private const string R2 = "2026-10-13T10-00-00-0002";
    private const string A = "2026-10-13-1234a";
    private const string B = "2026-10-13-1234b";
    private readonly TicketStoreHarness _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task TicketReviews_UnknownPair_Is404()
    {
        (await TicketReviewEndpoints.ReviewsAsync("alpha", "42", _store.Reviews, CancellationToken.None)).Should().BeOfType<NotFound>();
        (await TicketReviewEndpoints.ReviewsAsync("nope", "42", _store.Reviews, CancellationToken.None)).Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task TicketReviews_ApprovedNeverRun_ReturnsConversationRoundsNot404()
    {
        await _store.Approvals.SaveAsync(ApprovedSets.Record("github-42", ApprovedSets.Noon, tracker: "gh") with
        {
            ReviewRounds = [ApprovedReviewRoundsTests.Round(A, 1)],
        }, default);

        var view = await ReadAsync();

        view.Attempts.Should().BeEmpty();
        view.Conversation!.ApprovalAt.Should().Be(ApprovedSets.Noon);
        view.Conversation.Rounds.Should().ContainSingle().Which.SpecId.Should().Be(A);
    }

    [Fact]
    public async Task TicketReviews_RoundsGroupedByAttemptAndSpec_InEventOrder()
    {
        await _store.AddAsync(Run(R1, status: "failed"), Step(R1), Run(R2), Step(R2),
            Row(R1, 5, B, 1, 0, "build"), Row(R1, 3, A, 1, 0, "build-with-cuts"), Row(R1, 4, A, 1, 1, "build"),
            Row(R1, 6, A, 2, 0, "build"), Row(R2, 7, A, 1, 0, "refuse"));

        var view = await ReadAsync();

        view.Attempts.Select(a => (a.RunId, a.Number)).Should().Equal((R1, 1), (R2, 2));
        view.Attempts[0].Rounds.Select(r => (r.SpecId, r.Round, r.Reviewers.Count)).Should().Equal((A, 1, 2), (B, 1, 1), (A, 2, 1));
        view.Attempts[1].Rounds.Single().Verdict.Should().Be("refuse");
        view.Conversation.Should().BeNull("nobody approved this ticket in a conversation");
    }

    [Fact]
    public async Task TicketReviews_TwoReviewers_RoundVerdictIsTheMostSevere()
    {
        await _store.AddAsync(Run(R1), Step(R1), Row(R1, 3, A, 1, 0, "build"), Row(R1, 4, A, 1, 1, "refuse", revision: 2));

        var round = (await ReadAsync()).Attempts.Single().Rounds.Single();

        round.Verdict.Should().Be("refuse");
        round.Reviewers.Select(r => r.Verdict).Should().Equal("build", "refuse");
        round.RevisionNumber.Should().Be(2);
        round.Reviewers[1].Cuts.Single().Evidence.Should().Be("shop search:'Widget' [A2]", "evidence is rendered as stored");
        round.Reviewers[1].Residues.Single().Quote.Should().Be("old words");
        round.Reviewers[1].EvidenceProblems.Single().Problem.Should().Be("no such file");
    }

    [Fact]
    public async Task TicketReviews_SeriesRound_HasNoSpecIdAndCutsNameMembers()
    {
        await _store.AddAsync(Run(R1), Step(R1), Row(R1, 3, null, 1, 0, "build-with-cuts", SpecReviewScopes.Series, member: B));

        var round = (await ReadAsync()).Attempts.Single().Rounds.Single();

        round.Scope.Should().Be(SpecReviewScopes.Series);
        round.SpecId.Should().BeNull();
        round.Reviewers.Single().Cuts.Single().SpecId.Should().Be(B);
    }

    [Fact]
    public async Task TicketReviews_AttemptWithoutRounds_IsListedEmpty()
    {
        await _store.AddAsync(Run(R1), Step(R1), Run(R2), Step(R2), Row(R2, 3, A, 1, 0, "build"));

        var view = await ReadAsync();

        view.Attempts[0].Rounds.Should().BeEmpty();
        view.Attempts[1].Rounds.Should().ContainSingle();
    }

    [Fact]
    public async Task TicketReviews_PrReviewRun_IsNotAnAttempt()
    {
        await _store.AddAsync(Run(R1, pipeline: "pr-review"), Step(R1), Row(R1, 3, A, 1, 0, "build"), Run(R2), Step(R2));

        var view = await ReadAsync();

        view.Attempts.Should().ContainSingle().Which.Should().Match<TicketReviewAttemptView>(a => a.RunId == R2 && a.Number == 1);
    }

    [Fact]
    public async Task TicketReviews_ApprovedSet_ReturnsTheRecordsConversationRounds()
    {
        var epic = new EpicOutcome(Draft("parent"), [Draft("c-1"), Draft("c-2")])
        {
            ReviewRounds = [ApprovedReviewRoundsTests.Round("parent", 1), ApprovedReviewRoundsTests.Round("c-2", 1),
                ApprovedReviewRoundsTests.Round(ProposalReviewTokens.Series, 1) with { Cuts = [ApprovedReviewRoundsTests.Cut("c-1")] }],
        };
        await RecordAsync(ApprovedSetDoubles.SeriesFiling().Under("2026-10-14-0d0d", epic.Children, epic));
        await _store.AddAsync(Run(R1), Step(R1));

        var conversation = (await ReadAsync()).Conversation!;

        conversation.Rounds.Select(r => r.SpecId).Should().Equal("2026-10-14-0d0db", ProposalReviewTokens.Series);
        conversation.Rounds[1].Cuts.Single().SpecId.Should().Be("2026-10-14-0d0da", "a member button selects a filed spec");
    }

    [Fact]
    public async Task TicketReviews_ApprovedLoneSpec_ReturnsItsConversationRounds()
    {
        var spec = new SpecOutcome(Draft("p-1")) { ReviewRounds = [ApprovedReviewRoundsTests.Round("p-1", 1), ApprovedReviewRoundsTests.Round("p-1", 2)] };
        await RecordAsync(ApprovedSetDoubles.SeriesFiling().Under("2026-10-14-0d0d", [spec.Draft], spec));

        var rounds = (await ReadAsync()).Conversation!.Rounds;

        rounds.Select(r => (r.SpecId, r.Round)).Should().Equal(("2026-10-14-0d0da", 1), ("2026-10-14-0d0da", 2));
    }

    [Fact]
    public async Task TicketReviews_ConversationRound_FillsRoundLevelCutsAndReviewerVerdictsOnly()
    {
        await _store.Approvals.SaveAsync(ApprovedSets.Record("github-42", ApprovedSets.Noon, tracker: "gh") with
        {
            ReviewRounds = [ApprovedReviewRoundsTests.Round(A, 1) with { ReviewerVerdicts = ["refuse", null] }],
        }, default);

        var round = (await ReadAsync()).Conversation!.Rounds.Single();

        round.Scope.Should().Be(TicketReviewRoundView.Conversation);
        round.Cuts.Single().Replacement.Should().Be("the new goal");
        round.Findings.Single().Evidence.Should().Be("shop/src/A.cs:3 [A1]");
        round.Residues.Single().Quote.Should().Be("old words");
        round.Reviewers.Select(r => r.Verdict).Should().Equal("refuse", null);
        round.Reviewers.Should().OnlyContain(r => r.Cuts.Count == 0 && r.Findings.Count == 0 && r.Residues.Count == 0);
    }

    private async Task RecordAsync(FiledSeries series) =>
        await ApprovedSetDoubles.Recorder(_store.Approvals).RecordAsync(
            ContinuationHarness.State(), _store.Config.Projects["alpha"], "42", series, "goal", default);

    private static RunSpecReviewRound Row(
        string runId, long seq, string? specId, int round, int reviewer, string verdict,
        string scope = SpecReviewScopes.Spec, string? member = null, int? revision = null) => new()
    {
        RunId = runId, EventSeq = seq, SpecId = specId, Scope = scope, Round = round, Reviewer = reviewer, Verdict = verdict,
        At = T.AddMinutes(seq), RevisionNumber = revision,
        CutsJson = RunStoryJson.Serialize(new[] { ApprovedReviewRoundsTests.Cut(member) }),
        FindingsJson = RunStoryJson.Serialize(new[] { new SpecReviewFinding("A finding", "shop/src/A.cs:3 [A1]") }),
        EvidenceProblemsJson = RunStoryJson.Serialize(new[] { new SpecEvidenceProblem("A claim", "no such file") }),
        ResiduesJson = RunStoryJson.Serialize(new[] { new SpecReviewResidue("steps", "old words") }),
    };

    private static SpecDraft Draft(string id) => new(id, $"Goal of {id}", $"spec: {id}\ngoal: \"Goal of {id}\"\n", []) { Repos = ["api"] };

    private async Task<TicketReviewsView> ReadAsync() =>
        (await TicketReviewEndpoints.ReviewsAsync("alpha", "42", _store.Reviews, CancellationToken.None))
        .Should().BeOfType<Ok<TicketReviewsView>>().Subject.Value!;
}
