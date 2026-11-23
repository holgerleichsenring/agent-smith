using AgentSmith.Application.Services.Holds;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Series;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Series;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-23-8d16: the series detail marks each merge with the tickets that start only once it merged, from their
/// holds; and the bar's question follows the asking ticket's hold.</summary>
public sealed class SeriesRunNeededByTests
{
    private static readonly SeriesRunMergeStep[] Order =
    [
        new("42", "#42", 0, SeriesRunMergeWaits.Ready, []), new("43", "#43", 1, SeriesRunMergeWaits.After, ["#42"]),
        new("44", "#44", 1, SeriesRunMergeWaits.After, ["#42"]),
    ];

    private static SeriesRunTicketView Ticket(string id) => new(id, $"#{id}", "t", false, 0, [], [], false, null, []);

    private static TicketHold Starts(params string[] names) =>
        new(TicketHoldAddressees.Nobody, TicketHoldCases.StartsOnMerge, null, null) { Names = names };

    [Fact]
    public void Needed_EachStepNamesTheTicketsThatStartOnIt()
    {
        var holds = new Dictionary<string, TicketHold> { ["50"] = Starts("#43"), ["51"] = Starts("#42"), ["52"] = Starts("#60") };

        var steps = SeriesRunTickets.Needed(Order, [Ticket("42"), Ticket("43"), Ticket("44"), Ticket("50"), Ticket("51"), Ticket("52")], holds);

        steps.Select(s => s.NeededBy).Should().BeEquivalentTo(new[] { new[] { "#50", "#51" }, new[] { "#50" }, [] },
            o => o.WithStrictOrdering());
        SeriesRunTickets.Needed(Order, [Ticket("50")], null).Should().BeSameAs(Order, "without holds nothing is marked");
    }

    [Fact]
    public void Question_ARelayedOneWaitsOnlyWhileItsTicketsHoldIsTheQuestion()
    {
        var record = new SeriesRunRecord("sr-1", "c", "p", "u", true, SeriesRunStates.Running, [], DateTimeOffset.UnixEpoch)
        {
            Question = new SeriesQuestion("q", "Which tenant?", [], null, DateTimeOffset.UnixEpoch, null) { TicketId = "50" },
        };
        var asked = new Dictionary<string, TicketHold> { ["50"] = new(TicketHoldAddressees.Decide, TicketHoldCases.Question, null, null) };
        var working = new Dictionary<string, TicketHold> { ["50"] = new(TicketHoldAddressees.Nobody, TicketHoldCases.Working, null, null) };

        SeriesRunQuestion.Of(record, asked).Should().Be("Which tenant?");
        SeriesRunQuestion.Of(record, new Dictionary<string, TicketHold>
            { ["50"] = new(TicketHoldAddressees.Admin, TicketHoldCases.QuestionEnvironment, null, null) })
            .Should().Be("Which tenant?", "a park the environment and the plan answer is relayed and still waits");
        SeriesRunQuestion.Of(record, working).Should().BeNull("it was answered on the ticket's page");
        SeriesRunQuestion.Of(record, new Dictionary<string, TicketHold>()).Should().BeNull();
        SeriesRunQuestion.Of(record).Should().Be("Which tenant?", "without holds the record decides");
        SeriesRunQuestion.Of(record with { Question = record.Question! with { TicketId = null } }, working)
            .Should().Be("Which tenant?", "the master's own question is no ticket's hold");
    }
}
