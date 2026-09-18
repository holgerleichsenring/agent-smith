using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-15-a5a5: the ticket has three states — whole, truncated, absent — decided by
/// PRESENCE first, and a phase that states no criteria is shown with what it does state.
/// </summary>
public sealed class CutReviewWithoutTicketTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n")]
    public void Prompt_WithNoTicket_OffersTwoVerdictsAndNeverNotInTheTicket(string? ticket)
    {
        var prompt = SpecCutReviewPrompt.For(Criteria(), ticket, look: null);

        SpecCutReviewPrompt.StateOf(ticket).Should().Be(CutReviewTicket.Absent);
        prompt.Should().Contain("There are two ways:")
            .And.Contain("\"problem\": \"contradiction|uncheckable\"")
            .And.Contain("There is NO TICKET behind these phases")
            .And.NotContain("NOT IN THE TICKET")
            .And.NotContain("not-in-ticket")
            .And.NotContain("\nTICKET\n");
    }

    [Fact]
    public void Prompt_WithATruncatedTicket_IsUnchanged()
    {
        var prompt = SpecCutReviewPrompt.For(Criteria(), new string('x', 400_000), look: null);

        SpecCutReviewPrompt.StateOf(new string('x', 400_000)).Should().Be(CutReviewTicket.Truncated);
        prompt.Should().StartWith("A ticket has been cut into phases.")
            .And.Contain("There are two ways:")
            .And.Contain("TOO LONG TO SHOW")
            .And.Contain("\"problem\": \"contradiction|uncheckable\"")
            .And.Contain("\nTICKET\nxxx").And.EndWith("… ticket truncated")
            .And.NotContain("NOT IN THE TICKET");
    }

    [Fact]
    public void Prompt_WithAWholeTicket_IsUnchanged()
    {
        var prompt = SpecCutReviewPrompt.For(Criteria(), "migrate the messaging library", look: null);

        prompt.Should().StartWith("A ticket has been cut into phases.")
            .And.Contain("There are three ways:")
            .And.Contain("3. NOT IN THE TICKET")
            .And.Contain("\"problem\": \"contradiction|uncheckable|not-in-ticket\"")
            .And.EndWith("\nTICKET\nmigrate the messaging library")
            .And.NotContain("TOO LONG TO SHOW").And.NotContain("NO TICKET");
    }

    [Fact]
    public void Prompt_APhaseWithNoDoneList_RendersItsGoalAndStepActionsAsQuotable()
    {
        var prompt = SpecCutReviewPrompt.For([DoneLess()], null, look: null);

        prompt.Should().Contain(
            "phase_id: e1\ngoal: Move every sender onto the new bus\nsteps:\n"
            + "  - Replace the sender registration in the host\n  - Remove the legacy client package");
    }

    [Fact]
    public void Prompt_APhaseWithNoDoneList_RendersNoEmptyDoneHeading()
    {
        var prompt = SpecCutReviewPrompt.For([DoneLess(), DoneLess() with { Steps = [] }], null, look: null);

        prompt.Should().NotContain("done:");
        prompt.Should().NotContain("steps:\n\n", "a phase with no steps has no steps heading either");
    }

    [Fact]
    public void Prompt_Verdicts_DoNotAssumeACriterionExists()
    {
        var prompt = SpecCutReviewPrompt.For([DoneLess()], "the ticket", look: null);

        var verdicts = prompt[prompt.IndexOf("ways:", StringComparison.Ordinal)..prompt.IndexOf("Say nothing", StringComparison.Ordinal)];
        verdicts.Should().NotContainEquivalentOf("criterion").And.NotContainEquivalentOf("criteria");
        prompt.Should().Contain("its goal or one of\nits steps")
            .And.Contain("\"criterion\": \"<verbatim from the phase>\"")
            .And.Contain("<verbatim other statement of that phase, or null>");
    }

    [Fact]
    public void Prompt_ADoneLessDraft_IsNotJudgedAgainstCriteriaAlone()
    {
        var doneLess = SpecCutReviewPrompt.For([DoneLess()], null, look: null);
        var withCriteria = SpecCutReviewPrompt.For(Criteria(), "the ticket", look: null);

        doneLess.Should().NotContain("judged against its completion criteria")
            .And.Contain("judged against what it states");
        withCriteria.Should().Contain("judged against its completion criteria");
    }

    [Theory]
    [InlineData("upgrade", "", null, CutReviewTicket.Whole)]
    [InlineData("", "", "the audit is clean", CutReviewTicket.Whole)]
    [InlineData(" ", "", "  ", CutReviewTicket.Absent)]
    public void TicketText_TitleDescriptionAndCriteria_AreOneTicket(
        string title, string description, string? criteria, CutReviewTicket expected)
    {
        var ticket = new AgentSmith.Domain.Entities.Ticket(
            new AgentSmith.Domain.Models.TicketId("1"), title, description, criteria, "open", "test");

        SpecCutReviewPrompt.StateOf(SpecCutReviewTicketText.Of(ticket)).Should().Be(expected);
    }

    internal static PhaseDraft DoneLess() =>
        new("e1", "Move every sender onto the new bus", "phase: e1", [])
        {
            Steps =
            [
                new PhaseStep("register", "Replace the sender registration in the host", null),
                new PhaseStep("remove", "Remove the legacy client package", null),
            ],
        };

    private static IReadOnlyList<PhaseDraft> Criteria() =>
        [new PhaseDraft("p1a", "migrate the senders", "phase: p1a", []) { Done = ["every sender uses the new bus"] }];
}
