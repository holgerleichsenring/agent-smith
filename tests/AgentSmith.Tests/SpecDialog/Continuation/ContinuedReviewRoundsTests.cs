using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using static AgentSmith.Tests.SpecDialog.ApprovedReviewRoundsTests;

namespace AgentSmith.Tests.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-8762l: an approval in a ticket's own conversation re-keys the proposal's review rounds with the
/// map that gave its drafts their continued ids; a closed ticket's one-draft follow-up carries them too.
/// </summary>
public sealed class ContinuedReviewRoundsTests : IDisposable
{
    private const string Series = "2026-10-13-ab12";
    private static readonly string A = Series + "a", B = Series + "b", C = Series + "c", D = Series + "d";

    private readonly ContinuationHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task TicketContinuationAct_ProposalRounds_RekeyedOntoContinuedIds()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B, C], executed: [A]);
        await _h.Approvals.SaveAsync(new SpecApprovalRecord(ContinuationHarness.Key, new SpecSet(
            ContinuationHarness.Key, [], SpecAccounting.Empty, [], SpecSource.Approved, Series: Series), [], "gh")
        {
            ReviewRounds = [Round(A, 1), Round(C, 1)],
        }, default);
        var proposal = new EpicOutcome(Draft("parent"), [Draft(B), Draft("new-one")])
        {
            ReviewRounds = [Round("parent", 1), Round(B, 2), Round("new-one", 1),
                Round(ProposalReviewTokens.Series, 1) with { Cuts = [Cut("new-one")] }],
        };
        await _h.BindAsync(proposal);

        await _h.AcceptAsync(proposal);

        var rounds = (await _h.RecordAsync())!.ReviewRounds!;
        rounds.Select(r => (r.SpecId, r.Round)).Should().Equal((A, 1), (B, 2), (D, 1), (ProposalReviewTokens.Series, 1));
        rounds[^1].Cuts.Single().SpecId.Should().Be(D);
    }

    [Fact]
    public async Task ClosedTicketFollowUp_SingleDraftLeft_CarriesTheProposalRounds()
    {
        var proposal = new EpicOutcome(Draft("parent"), [Draft("p-1"), Draft("p-2")])
        {
            ReviewRounds = [Round("p-1", 1), Round("p-2", 1)],
        };
        var decision = new TicketContinuationDecision(
            TicketContinuationEffects.FollowUpClosed, "Ticket 42 was closed by a person.", 1, 1, "r-1", [], [proposal.Children[1]]);

        var outcome = await _h.InScopeAsync(sp => sp.GetRequiredService<ClosedTicketFollowUp>().FileAsync(
            ContinuationHarness.State(), proposal, decision, _h.Config.Projects["alpha"], ContinuationHarness.TicketId, false, default));

        outcome.Filed.Should().BeOfType<SpecOutcome>().Which.ReviewRounds.Should().BeEquivalentTo(proposal.ReviewRounds);
        var filed = (await _h.Approvals.GetAsync("gh", "github-43", default))!;
        filed.ReviewRounds!.Should().ContainSingle("p-1 ran on the closed ticket and is not filed again")
            .Which.SpecId.Should().Be(filed.Set.Specs.Single().SpecId);
    }

    private static SpecDraft Draft(string id) =>
        new(id, $"Goal of {id}", $"spec: {id}\ngoal: \"Goal of {id}\"\ndone:\n  - \"{id} works.\"\n", []) { Repos = ["api"], Done = [$"{id} works."] };
}
