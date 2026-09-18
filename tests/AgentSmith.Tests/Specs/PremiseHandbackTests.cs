using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using FluentAssertions;
using static AgentSmith.Tests.Specs.PremiseCheckTestDoubles;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-0e79c: what a false premise DOES. The step fails, the phase row is written —
/// between SelectPhase and verification nothing else would write it — and the ticket is told,
/// because a run that delivers its verified phases as a shortfall never reaches the error
/// path's own failure comment.
/// </summary>
public sealed class PremiseHandbackTests
{
    private const string Falsified =
        """
        [{"premise": "OrderHandler validates the payload before dispatch",
          "verdict": "no-longer-holds", "why": "nothing in the handler validates any more",
          "cites": "M1"}]
        """;

    [Fact]
    public async Task PremiseCheck_FalsePremise_RecordsThePhaseFailedWithTheVerdictInOneArgument()
    {
        var h = For(Draft(), Falsified, searches: (DerivationTestLooks.Repo, "Validate"));

        await h.RunAsync();

        // The recorder publishes failingCommand ?? note, so a verdict split over the two
        // arguments would lose half of itself silently.
        h.Verdict.Should().NotBeNull()
            .And.Contain("OrderHandler validates the payload before dispatch")
            .And.Contain("nothing in the handler validates any more")
            .And.Contain("[M1]");
    }

    [Fact]
    public async Task PremiseCheck_FalsePremise_TheVerdictReachesTheRowNotJustTheLog()
    {
        var h = For(Draft(), Falsified, searches: (DerivationTestLooks.Repo, "Validate"));

        await h.RunAsync();

        var row = h.Pipeline.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress)
            .Phases.Single(p => p.PhaseId == PhaseId);
        row.State.Should().Be(PhaseRunState.HandedBack,
            "a step failing between SelectPhase and verification would leave it in progress forever");
        row.FailingCommand.Should().Contain("False premise in " + PhaseId);
    }

    [Fact]
    public async Task PremiseCheck_FalsePremise_TicketCarriesTheFindingAndTheAmendmentPath()
    {
        var h = For(Draft(), Falsified, searches: (DerivationTestLooks.Repo, "Validate"));

        await h.RunAsync();

        h.Ticket.Comments.Should().ContainSingle().Which
            .Should().Contain(PremiseHandback.Heading)
            .And.Contain("OrderHandler validates the payload before dispatch")
            .And.Contain("it looked at:")
            .And.Contain("[M1]")
            .And.Contain(ApprovedSetKept.WhereToChangeItWithNoPullRequest,
                "where a change is actually made, in the one wording the run already uses");
        h.Decisions.Decisions.Should().ContainSingle(
            "the run carries it too, for a run whose tracker refuses the comment");
    }

    [Fact]
    public async Task PremiseCheck_FalsePremiseBeforeAnyPullRequest_DoesNotSendTheReaderToOne()
    {
        var h = For(Draft(), Falsified, searches: (DerivationTestLooks.Repo, "Validate"));

        await h.RunAsync();

        h.Ticket.Comments.Single().Should().NotContain("open in the pull request",
            "a hand-back on the first phase of a set happens before any delivery");
    }

    [Fact]
    public async Task PremiseCheck_FalsePremiseAfterAPullRequest_NamesIt()
    {
        var h = For(Draft(), Falsified, searches: (DerivationTestLooks.Repo, "Validate"));
        h.Pipeline.Set(ContextKeys.SpecPullRequestUrl, "https://example.invalid/pr/7");

        await h.RunAsync();

        h.Ticket.Comments.Single().Should().Contain(ApprovedSetKept.WhereToChangeIt);
    }

    [Fact]
    public async Task PremiseCheck_RetriggerWithTheSameFinding_DoesNotSayItAgain()
    {
        // A phase is marked executed only at WritePhaseRecord, so every re-trigger re-enters it
        // and re-asks. Five re-triggers used to leave five identical comments.
        var h = For(Draft(), Falsified, searches: (DerivationTestLooks.Repo, "Validate"));
        await h.RunAsync();
        var told = h.Ticket.Comments.Single();
        WithComment(h.Pipeline, told);

        var again = For(Draft(), Falsified, searches: (DerivationTestLooks.Repo, "Validate"));
        WithComment(again.Pipeline, told);
        var result = await again.RunAsync();

        again.Ticket.Comments.Should().BeEmpty("the ticket was already told and nobody replied");
        result.IsSuccess.Should().BeFalse("it is still the verdict — only the comment is not repeated");
        again.Verdict.Should().NotBeNull("and the phase row still carries it");
    }

    [Fact]
    public async Task PremiseCheck_RetriggerAfterSomebodyReplied_SaysItAgain()
    {
        var h = For(Draft(), Falsified, searches: (DerivationTestLooks.Repo, "Validate"));
        await h.RunAsync();
        var again = For(Draft(), Falsified, searches: (DerivationTestLooks.Repo, "Validate"));
        WithComment(again.Pipeline, h.Ticket.Comments.Single());
        WithComment(again.Pipeline, "I have fixed the spec, try again", byUs: false);

        await again.RunAsync();

        again.Ticket.Comments.Should().ContainSingle(
            "somebody engaged, so answering them is not repeating yourself");
    }

    [Fact]
    public async Task PremiseCheck_FalsePremise_ATrackerThatRefusesStillFailsTheStep()
    {
        var h = For(Draft(), Falsified, searches: (DerivationTestLooks.Repo, "Validate"));
        h.Ticket.Refuse = true;

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeFalse("the verdict does not depend on a tracker answering");
        h.Verdict.Should().NotBeNull();
    }

    [Fact]
    public void PremiseCheck_Verdict_IsOneLineEvenWhenThePremiseIsProse()
    {
        var verdict = PremiseHandback.Verdict(
            PhaseId,
            new AgentSmith.Domain.Models.PremiseFinding(
                "a premise\nover two lines", PremiseCheckPrompt.NoLongerHolds, "because\r\nso",
                "M1", "[M1] Sample.Server: the premise check ran 'grep' exited 1"));

        verdict.Should().NotContain("\n").And.NotContain("\r");
        verdict.Should().Contain("[M1]");
    }
}
