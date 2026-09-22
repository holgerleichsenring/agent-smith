using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-22-4d17: the specification pointer a filed work ticket carries. It records WHICH
/// conversation approved the cut — provenance, and true — and it no longer tells its reader that
/// a change to the approved set is made there, because nothing re-opens an approved record from
/// a conversation.
/// </summary>
public sealed class FiledTicketSpecificationTests
{
    private const string Yaml = """
        phase: p9000a
        goal: Widget storage layer
        scope:
          in: >
            The table, the migration and the repository that reads it.
          out: >
            THE API ON TOP. Its own slice, and it needs this one first.
        decisions:
          - key: |
              A TABLE BEFORE AN API, BECAUSE THE API SHAPE FOLLOWS THE STORAGE.
        steps:
          - id: the-table
            action: "Add the table and its migration."
        done:
          - "the table exists and the repository reads it"
        """;

    private static readonly PhaseDraft Draft = new("p9000a", "Widget storage layer", Yaml, []);

    [Fact]
    public void FiledTicket_AnApprovedCut_StillRecordsTheConversationItWasApprovedIn() =>
        Render("sess-4d17").Should().Contain("Approved in design conversation `sess-4d17`.",
            "which conversation approved the cut is provenance worth keeping");

    [Fact]
    public void FiledTicket_ASpecificationSection_DoesNotSayAChangeIsMadeInTheConversation() =>
        Render("sess-4d17").Should().NotContain("where a change to it is made",
            "nothing re-opens an approved record from a design conversation, so a reader sent "
            + "there finds no way in");

    [Fact]
    public void FiledTicket_ATicketFiledWithNoConversation_IsUnchanged()
    {
        var body = Render(conversation: null);

        body.Should().Contain(PhaseTicketRenderer.SpecificationHeading)
            .And.Contain(SpecSetKey.Root, "the run reads the set from the branch")
            .And.NotContain("Approved in design conversation",
                "a ticket filed without a conversation never named one");
    }

    [Fact]
    public void FiledTicket_TheSpecificationSection_IsStillAfterTheAcceptanceCriteria()
    {
        var body = Render("sess-4d17");

        body.IndexOf(PhaseTicketRenderer.SpecificationHeading, StringComparison.Ordinal)
            .Should().BeGreaterThan(
                body.IndexOf(AcceptanceCriteriaSection.Heading, StringComparison.Ordinal),
                "the acceptance-criteria reader stops at the next heading, so the pointer must "
                + "stay behind it");
    }

    private static string Render(string? conversation) =>
        new PhaseTicketRenderer().RenderPhase(Draft, conversation).Body;
}
