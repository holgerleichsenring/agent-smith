using AgentSmith.Application.Services.Prompts;
using AgentSmith.Domain.Entities;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

/// <summary>
/// p0424: the conversation section renders what the OPERATOR said.
/// <para>
/// Ticket 19106's thread had grown to 147,462 characters — 97% of a 152k user message,
/// re-sent on every one of 115 rounds — and almost all of it was agent-smith's own
/// announcements from twenty-four runs. That phase made 348 tool calls and wrote nothing
/// in four hours: an agent reading its own past output as instruction spends its rounds
/// reconciling versions of the task instead of doing it.
/// </para>
/// </summary>
public sealed class TicketConversationDietTests
{
    [Fact]
    public void OurOwnAnnouncements_AreNotFedBackAsInstruction()
    {
        var rendered = TicketConversationPromptSection.Render([
            Comment("agent-smith", "## Agent Smith — this is how I understood the ticket\nphases…"),
            Comment("agent-smith", "<b>Agent Smith — Failed</b><br/>build red"),
            Comment("operator", "Please also cover the worker repository."),
        ]);

        rendered.Should().Contain("cover the worker repository");
        rendered.Should().NotContain("this is how I understood the ticket",
            "our own echo is not the operator's instruction");
    }

    [Fact]
    public void AQuestionTheOperatorAnswered_IsKept_BecauseTheAnswerNeedsIt()
    {
        var rendered = TicketConversationPromptSection.Render([
            Comment("agent-smith", "<!--agent-smith:open-questions-->Q1: inventory or migrate?"),
            Comment("operator", "Q1: migrate"),
        ]);

        rendered.Should().Contain("inventory or migrate",
            "\"Q1: migrate\" means nothing without the question it answers");
        rendered.Should().Contain("Q1: migrate");
    }

    [Fact]
    public void ALongHistory_IsCutToTheRecentThread_AndSaysSo()
    {
        var comments = Enumerable.Range(0, 60)
            .Select(i => Comment("operator", $"comment {i} " + new string('x', 2_000)))
            .ToList();

        var rendered = TicketConversationPromptSection.Render(comments);

        rendered.Length.Should().BeLessThan(TicketConversationPromptSection.MaxChars + 2_000);
        rendered.Should().Contain("comment 59", "the operator's latest word is the one in force");
        rendered.Should().Contain("earlier comment(s) omitted",
            "a silently shortened thread is the kind of missing context nobody can debug");
    }

    [Fact]
    public void NoConversation_RendersNothing()
    {
        TicketConversationPromptSection.Render([]).Should().BeEmpty();
    }

    private static int order;

    private static TicketComment Comment(string author, string body) =>
        new(author, DateTimeOffset.UnixEpoch.AddMinutes(order++), body);
}
