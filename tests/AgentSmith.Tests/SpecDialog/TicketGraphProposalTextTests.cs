using System.Text.Json;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Handlers;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-14-dbcek: a proposed list of tickets in chat is the compact list in merge order, the pane carries the
/// texts, and Slack's section limit never stops a confirmation from posting.
/// </summary>
public sealed class TicketGraphProposalTextTests
{
    private static GraphTicket Ticket(string key, int done, params string[] requires) =>
        new(key, $"Title of {key}", $"The scope of {key}.", [.. Enumerable.Range(1, done).Select(i => $"{key} done {i}")], requires, ["api"]);

    // Proposed out of order: the dependent first.
    private static TicketGraphOutcome List() =>
        new([Ticket("order-ui", 2, "order-api", "ticket:#41"), Ticket("order-api", 1), Ticket("order-export", 3, "order-ui")]);

    private static string Confirmation(OutcomeProposal proposal) =>
        new SpecDialogOutcomeComposer().ComposeConfirmation(proposal).In(SpecDialogMarkup.For("slack"));

    [Fact]
    public void SpecDialogOutcomeComposer_TicketGraph_ConfirmationListsTicketsInMergeOrderWithWaitsFor()
    {
        var text = Confirmation(List());

        text.Should().StartWith("Proposed outcome: *3 tickets*, in the order they must merge\n"
            + "1. `order-api` Title of order-api — 1 done item; nothing to wait for\n"
            + "2. `order-ui` Title of order-ui — 2 done items; waits for order-api, #41\n"
            + "3. `order-export` Title of order-export — 3 done items; waits for order-ui\n"
            + "The full ticket texts are on the dashboard's proposal pane.");
        text.Should().NotContain("The scope of", "chat gets the compact list, the pane the texts");
    }

    [Fact]
    public void SpecDialogOutcomeComposer_JiraKeyRequirement_ShownAsWritten() =>
        Confirmation(new TicketGraphOutcome([Ticket("order-api", 1, "ticket:PROJ-41"), Ticket("order-ui", 1, "order-api")]))
            .Should().Contain("waits for PROJ-41").And.NotContain("#PROJ-41");

    [Fact]
    public void SpecDialogOutcomeComposer_Epic_ConfirmationTextUnchanged()
    {
        var parent = new SpecDraft("p9000", "rebuild the export", "spec: p9000", []);
        var child = new SpecDraft("p9000b", "write the file", "spec: p9000b", []) { Requires = ["p9000a"] };
        var epic = new EpicOutcome(parent, [new SpecDraft("p9000a", "read the orders", "spec: p9000a", []), child]);

        Confirmation(epic).Should().StartWith("Proposed outcome: *epic* `p9000` — rebuild the export\n"
            + "Slices, in order:\n- `p9000a` read the orders\n- `p9000b` write the file (requires: p9000a)\n");
    }

    [Fact]
    public void SpecDialogOutcomeComposer_TicketGraph_TwentyTickets_ChatTextUnder3000Chars()
    {
        var reply = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Validation", "fixtures", "ticket-graph-20-tickets.md"));
        var graph = (TicketGraphOutcome)((OutcomeResolved)MasterHandlerFixture.BuildOutcomeResolver().Resolve(reply, ["api", "web"])).Proposal;

        var text = Confirmation(graph);

        graph.Tickets.Should().HaveCount(20);
        text.Length.Should().BeLessThan(3000);
        SlackTypedQuestionBlockBuilder.Section(":clipboard:", text).Should().EndWith("*", "it posts whole, unclipped");
    }

    [Fact]
    public void SlackTypedQuestionBlockBuilder_ApprovalTextOver3000_ClippedAtALineEndNamingTheDashboard()
    {
        var text = string.Join("\n", Enumerable.Range(1, 60).Select(i => $"{i}. `ticket-{i}` " + new string('x', 80)));
        var question = new DialogQuestion("q-1", QuestionType.Approval, text, null, [], "", TimeSpan.FromSeconds(30));

        var json = JsonSerializer.Serialize(new SlackTypedQuestionBlockBuilder().Build(question));
        var section = JsonDocument.Parse(json).RootElement[0].GetProperty("text").GetProperty("text").GetString()!;

        section.Length.Should().BeLessThanOrEqualTo(3000);
        section.Should().StartWith(":clipboard: *1. `ticket-1`").And.EndWith("*\n… the rest is on the dashboard");
        section[..section.IndexOf("*\n… the rest", StringComparison.Ordinal)].Should().EndWith(new string('x', 80), "cut at a line end");
    }

    [Fact]
    public void SpecDialogProposalComposer_TicketGraph_PushKindTicketsWithOrderedTicketTexts()
    {
        var composer = new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer());

        var push = composer.Compose("d-1", List(), DateTimeOffset.UnixEpoch)!;

        push.Kind.Should().Be(SpecDialogProposalComposer.TicketsKind).And.Be("tickets");
        SpecDialogProposalComposer.KindOf(List()).Should().Be("tickets");
        push.Tickets.Select(t => t.Key).Should().Equal("order-api", "order-ui", "order-export");
        var ui = push.Tickets[1];
        (ui.Title, ui.Requirement).Should().Be(("Title of order-ui", "The scope of order-ui."));
        ui.Done.Should().Equal("order-ui done 1", "order-ui done 2");
        ui.Requires.Should().Equal("order-api", "ticket:#41");
        ui.Repos.Should().Equal("api");
        push.Children.Should().BeEmpty();
    }
}
