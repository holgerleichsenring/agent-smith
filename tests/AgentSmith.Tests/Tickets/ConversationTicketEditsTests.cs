using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets.Conversation;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets;

/// <summary>
/// 2026-11-22-e812: an attempt that rewrote its ticket's text from the approved specs left that text behind; the next
/// attempt fetching it is no edit, and a person's later edit is diffed against what agent-smith wrote.
/// </summary>
public sealed class ConversationTicketEditsTests
{
    private static readonly DateTimeOffset T = new(2026, 11, 22, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Of_NextAttemptFetchesTheRewrittenText_ShowsNoEdit()
    {
        var edits = ConversationTicketEdits.Of(Runs(), Rows(secondFetched: "written")).ToList();

        edits.Should().BeEmpty("agent-smith's own rewrite is not a person's edit");
    }

    [Fact]
    public void Of_PersonEditedAfterTheRewrite_ShowsTheEditAgainstTheRewrittenText()
    {
        var edit = ConversationTicketEdits.Of(Runs(), Rows(secondFetched: "edited")).Should().ContainSingle().Subject;

        edit.Kind.Should().Be(TicketConversationKinds.Edited);
        edit.Role.Should().Be(TicketConversationRoles.Person);
        System.Text.Json.JsonSerializer.Serialize(edit.Detail).Should().Contain("Rewritten from the specs")
            .And.NotContain("Fetched before the rewrite");
    }

    private static List<Run> Runs() =>
    [
        new() { Id = "r-1", Pipeline = "code", Status = "success", StartedAt = T, TicketTitle = "t" },
        new() { Id = "r-2", Pipeline = "code", Status = "running", StartedAt = T.AddHours(1), TicketTitle = "t" },
    ];

    private static ConversationRunRows Rows(string secondFetched) => new(
        [
            Entry("r-1", RunConversationKinds.TicketText, "Fetched before the rewrite", 1),
            Entry("r-1", RunConversationKinds.TicketRewritten, "Rewritten from the specs", 5),
            Entry("r-2", RunConversationKinds.TicketText, secondFetched == "written" ? "Rewritten from the specs" : "Changed by a person", 1),
        ],
        [
            new RunAttempt { RunId = "r-1", TicketFingerprint = "fetched", LeftTicketFingerprint = "written" },
            new RunAttempt { RunId = "r-2", TicketFingerprint = secondFetched },
        ],
        []);

    private static RunConversationEntry Entry(string runId, string kind, string text, long seq) =>
        new() { RunId = runId, Kind = kind, Text = text, EventSeq = seq, At = T.AddMinutes(seq) };
}
