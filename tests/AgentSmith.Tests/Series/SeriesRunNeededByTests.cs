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

    // 2026-11-27-3532b: only the master's own question waits on a person; a ticket's relayed from before is none.
    [Fact]
    public void Question_OnlyTheMastersOwnWaits_ARelayedOneFromBeforeIsNone()
    {
        var record = new SeriesRunRecord("sr-1", "c", "p", "u", true, SeriesRunStates.Running, [], DateTimeOffset.UnixEpoch)
        {
            Question = new SeriesQuestion("q", "Which tenant?", [], null, DateTimeOffset.UnixEpoch, null) { TicketId = "50" },
        };

        SeriesRunQuestion.Of(record).Should().BeNull("the next beat drops it and the master is asked it");
        SeriesRunQuestion.Of(record with { Question = record.Question! with { TicketId = null } }).Should().Be("Which tenant?");
        SeriesRunQuestion.Of(record with { Question = record.Question! with { TicketId = null, Answer = "t1" } }).Should().BeNull();
    }
}
