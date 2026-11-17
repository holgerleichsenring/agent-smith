using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets.Conversation;
using FluentAssertions;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.Conversation;

/// <summary>2026-11-17-dd05a: a posted cut comment reads as one line on What happened; nothing else changes.</summary>
public sealed class SpecSetCommentLineTests
{
    private static readonly string Derived = $"{SpecSetComment.Marker}\n## Agent Smith — {SpecSetComment.CutMarker}\n\n"
        + "I split it into the specs below.\n\n## Derived specs\n\n### s1 — First\n\n**Done when:**\n- a\n\n### s2 — Second\n\n**Done when:**\n- b\n";

    private static readonly string Approved = Derived.Replace(
        "I split it into the specs below.", SpecSetCommentPreamble.ApprovedOpening + " and I started working.", StringComparison.Ordinal);

    private static TicketConversationEntryView Entry(string text, bool ours = true, string format = TicketTextFormat.Markdown,
        string source = TicketConversationSources.Ticket, int minutes = 10) =>
        new("ticket:c-1", "c-1", null, source, TicketConversationKinds.Comment, TicketConversationRoles.Agent, "agent",
            ours, T.AddMinutes(minutes), "r-1", null, text, Format: format);

    private static RunAttempt Attempt(int? revision = 5, string? conversation = null) =>
        new() { RunId = "r-1", RevisionNumber = revision, ApprovalConversation = conversation };

    [Fact]
    public void Of_DerivedCutInsideItsRun_CountAndRevision()
    {
        var line = SpecSetCommentLine.Of(Entry(Derived), Attempt(), Run("r-1"));

        (line.Kind, line.Text, line.Format).Should().Be((TicketConversationKinds.SpecSet, "2 specs derived, revision 5", TicketTextFormat.Text));
    }

    [Fact]
    public void Of_CutAfterItsRunEnded_NoRevision()
    {
        SpecSetCommentLine.Of(Entry(Derived, minutes: 45), Attempt(), Run("r-1")).Text
            .Should().Be("2 specs derived", "a spec-derive run's comment lands under an earlier attempt it does not belong to");
    }

    [Fact]
    public void Of_ApprovedFromAConversation_SaysWorkItOut()
    {
        SpecSetCommentLine.Of(Entry(Approved), Attempt(1, "conv-1"), Run("r-1")).Text
            .Should().Be("2 specs approved from Work it out, revision 1");
    }

    [Fact]
    public void Of_ApprovedWithoutAConversation_SaysApproved()
    {
        SpecSetCommentLine.Of(Entry(Approved), Attempt(2), Run("r-1")).Text.Should().Be("2 specs approved, revision 2");
    }

    [Fact]
    public void Of_HtmlBody_ReadAsText()
    {
        var html = $"<div>{SpecSetComment.Marker}<h2>Agent Smith &#8212; {SpecSetComment.CutMarker}</h2>"
            + "<p>I split it.</p><h3>s1 — First</h3><p><strong>Done when:</strong></p><ul><li>a</li></ul></div>";

        SpecSetCommentLine.Of(Entry(html, format: TicketTextFormat.Html), Attempt(3), Run("r-1")).Text
            .Should().Be("1 spec derived, revision 3");
    }

    [Fact]
    public void Of_ReplyQuotingTheCut_StaysWhole()
    {
        var reply = "> " + Derived.Replace("\n", "\n> ", StringComparison.Ordinal) + "\nThe second spec is wrong.";

        SpecSetCommentLine.Of(Entry(reply), Attempt(), Run("r-1")).Should().Match<TicketConversationEntryView>(
            e => e.Kind == TicketConversationKinds.Comment && e.Text == reply);
    }

    [Fact]
    public void Of_PersonsCommentOrOtherSource_Untouched()
    {
        var persons = Entry(Derived, ours: false);
        var cut = Entry(Derived, source: TicketConversationSources.SpecCut);

        SpecSetCommentLine.Of(persons, Attempt(), Run("r-1")).Should().BeSameAs(persons);
        SpecSetCommentLine.Of(cut, Attempt(), Run("r-1")).Should().BeSameAs(cut);
    }
}
