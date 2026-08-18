using AgentSmith.Application.Services.Prompts;
using AgentSmith.Domain.Entities;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

/// <summary>
/// p0448: a notice about a RUN is not something the ticket says about the WORK.
/// <para>
/// p0424 drops our own announcements — except the newest one, so an outstanding question
/// survives into the next run. Live run a1e1 is what that exception costs. The operator
/// cancelled the run before it, which posted "Agent Smith — Cancelled / Cancelled by
/// operator." That was the only comment on the ticket, so it was ours, it was newest, and
/// it became the ENTIRE conversation the derivation was shown. The derivation handed the
/// ticket back: "The operator cancelled the migration, so it cannot be implemented as an
/// active ticket."
/// </para>
/// <para>
/// It read correctly. The comment says exactly that. A cancelled run poisoned its own
/// ticket, and the handback it then wrote is itself a verdict about the work — so a retry
/// reads that instead and hands back again. The exception belongs to questions, which are
/// the only comments of ours that are still waiting for the operator.
/// </para>
/// </summary>
public sealed class TicketRunNoticeTests
{
    [Fact]
    public void ACancelledRunsNotice_IsNotWhatTheTicketAsksFor()
    {
        var rendered = TicketConversationPromptSection.Render([
            Comment("agent-smith", "<b>Agent Smith — Cancelled</b><br/>Cancelled by operator."),
        ]);

        rendered.Should().BeEmpty(
            "a run was cancelled; the work was not withdrawn, and nobody said it was");
    }

    [Fact]
    public void OurOwnVerdict_IsNotReadBackAsTheRequirement()
    {
        var rendered = TicketConversationPromptSection.Render([
            Comment("agent-smith",
                "## Agent Smith — not implementable as specified\n\nThe operator cancelled "
                + "the migration, so it cannot be implemented as an active ticket."),
        ]);

        rendered.Should().BeEmpty(
            "a handback that reads itself back hands back again, every time");
    }

    [Fact]
    public void AnOutstandingQuestion_StillReachesTheNextRun()
    {
        var rendered = TicketConversationPromptSection.Render([
            Comment("operator", "Migrate the messaging library."),
            Comment("agent-smith", "**Agent Smith — open questions**\nQ1: may I raise the pins?"),
        ]);

        rendered.Should().Contain("may I raise the pins",
            "the next run must not ask what is already on the ticket unanswered");
    }

    [Fact]
    public void ANoticeTheOperatorRepliedTo_KeepsItsContext()
    {
        var rendered = TicketConversationPromptSection.Render([
            Comment("agent-smith", "<b>Agent Smith — Failed</b><br/>the build was red"),
            Comment("operator", "That was my fault, the feed was down. Try again."),
        ]);

        rendered.Should().Contain("the build was red",
            "\"that was my fault\" means nothing without what it answers");
    }

    private static int order;

    private static TicketComment Comment(string author, string body) =>
        new(author, DateTimeOffset.UnixEpoch.AddMinutes(order++), body);
}
