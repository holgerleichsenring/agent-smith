using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.PipelineHarness.Llm;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>2026-11-12-cd4ce: a design turn ends when the model stops; its kind is what its draft holds.</summary>
public sealed partial class SpecDialogOutcomeTests
{
    [Fact]
    public async Task DesignTurn_EndsWithWritesAndNoProse_ProposesTheDraftAndSaysWhatItHolds()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        harness.ChatClient
            .EnqueueTicketWrite("orders-schema", "Add the order status column", "Add a status column.", ["the column exists"])
            .EnqueueTicketWrite("orders-filter", "Filter orders by status", "Filter by the new column.", ["the filter works"], ["orders-schema"])
            .EnqueueText(string.Empty)
            .EnqueueText("[]") // the plan reviewer finds nothing
            .EnqueueText(string.Empty); // 2026-11-13-67ae: the master reads the clean report and stops again

        var result = await RunTurnAsync(harness, Discussed("cut it into tickets"));

        result.Outcome.Should().BeOfType<TicketGraphOutcome>().Which.Tickets.Select(t => t.Key)
            .Should().Equal("orders-schema", "orders-filter");
        result.Reply.Should().Be("The draft holds 2 ticket(s).");
        harness.ChatClient.CallMessages.Select(call => call.Last().Text)
            .Should().Contain(text => text.Contains(DesignTurnReviewReport.CleanList));
    }
}
