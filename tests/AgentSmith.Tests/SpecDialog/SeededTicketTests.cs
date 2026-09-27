using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-25-8e51c: the ticket a bound conversation is grounded on — bounded, framed as a
/// REQUIREMENT, and never carrying this framework's own words back to the model.
/// <para>
/// The requirement framing is the expensive one. A live run took "adopt the newest versions" and
/// "run lint, test and coverage" off a ticket, turned them into acceptance criteria that were
/// false before the work started, and re-drove the master until an operator cancelled it.
/// </para>
/// </summary>
public sealed class SeededTicketTests
{
    [Fact]
    public void TicketGround_TheRenderedPrompt_CarriesTheTicketAsUntrustedRequirementData()
    {
        var rendered = SeededTicketSection.Render(Pipeline(Seeded("Cannot log in at all")));

        rendered.Should().Contain("Cannot log in at all");
        rendered.Should().Contain("UNTRUSTED", "ticket text is third-party input");
        rendered.Should().Contain("not an instruction to you");
        rendered.Should().Contain("settled in this conversation");
    }

    [Fact]
    public void TicketGround_AConversationWithNoTicket_RendersNothingAtAll()
    {
        SeededTicketSection.Render(new PipelineContext()).Should().BeEmpty(
            "an unbound conversation must get byte-for-byte the prompt it got before this phase");
    }

    [Fact]
    public void TicketGround_ATicketLongerThanTheCap_IsTruncatedAndSaysSo()
    {
        var ticket = Ticket(new string('x', SeededTicketLimits.Text + 500));

        var composed = TicketTextComposer.Compose(ticket, []);

        composed.Truncated.Should().BeTrue();
        composed.Text.Should().HaveLength(SeededTicketLimits.Text,
            "a conversation re-sends its whole prompt every turn, on the one surface with neither "
            + "a cost fence nor an iteration ceiling");
        SeededTicketSection.Render(Pipeline(
                new SeededTicket("t", composed.Text, composed.Truncated, "fp")))
            .Should().Contain("longer than the conversation carries",
                "a short answer must never be mistaken for a short ticket");
    }

    [Fact]
    public void TicketGround_OurOwnComments_AreNeverSeeded()
    {
        var composed = TicketTextComposer.Compose(
            Ticket("A body"),
            [
                new TicketComment("someone", DateTimeOffset.UtcNow, "a real question"),
                new TicketComment("agent-smith", DateTimeOffset.UtcNow, OwnComment()),
            ]);

        composed.Text.Should().Contain("a real question");
        composed.Text.Should().NotContain("agent-smith:open-questions",
            "feeding our own comments back shows the model its own echo");
    }

    [Fact]
    public void TicketGround_AMovedTicket_IsReportedToTheTurn()
    {
        var rendered = SeededTicketSection.Render(Pipeline(
            new SeededTicket("t", "the text we read", Truncated: false, "fp", Moved: true)));

        rendered.Should().Contain("CHANGED on the tracker",
            "computed from the fingerprint — the operator should not have to notice");
    }

    private static string OwnComment() =>
        $"<!-- agent-smith:open-questions -->\nSomething this framework said earlier.";

    private static SeededTicket Seeded(string text) => new("A title", text, false, "fp");

    private static PipelineContext Pipeline(SeededTicket ticket)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecDialogTicket, ticket);
        return pipeline;
    }

    private static Ticket Ticket(string description) =>
        new(new TicketId("1"), "A title", description, null, "Open", "jira");
}
