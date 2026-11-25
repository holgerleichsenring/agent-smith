using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Series;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-9992f: add_ticket and link_tickets reach the edit acts with the members' ids; an added ticket becomes a
/// member the beat's next call can name and the act's ticket; an unknown ticket or a missing runs.control acts on nothing.</summary>
public sealed class SeriesEditToolsTests
{
    private readonly Mock<ISeriesEditActs> _edits = new();
    private readonly InMemorySeriesRunStore _store = new();
    private SeriesBeat _beat = null!;

    private async Task<SeriesEditTools> ToolsAsync(bool mayStartRuns = true)
    {
        var record = SeriesRecords.Running(mayStartRuns: mayStartRuns);
        await _store.OpenAsync(record, default);
        _beat = new SeriesBeat(record, [.. record.Tickets.Select(t => new SeriesTicketFact(t))], SeriesRecords.T);
        return new SeriesEditTools(_beat, _edits.Object, _store);
    }

    [Fact]
    public async Task AddTicket_ReachesTheActWithIds_AndTheNewTicketIsAMemberOfTheBeat()
    {
        var tools = await ToolsAsync();
        _edits.Setup(e => e.AddTicketAsync(It.IsAny<SeriesRunRecord>(), "Index", "add it", new[] { "43" }, new[] { "44" }, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeriesCutAnswer("Filed SCRUM-46.", true, "46", "SCRUM-46"));

        var said = await tools.AddTicketAsync("Index", "add it", ["SCRUM-43"], ["44"], "44 needs the index first");

        said.Should().Be("Filed SCRUM-46.");
        SeriesTicketLookup.Find(_beat, "SCRUM-46")!.Requires.Should().Equal("43");
        (_beat.ActTicket, _beat.ActDid).Should().Be(("46", "added SCRUM-46"));
        _beat.Said.Should().ContainSingle().Which.Should().EndWith("added SCRUM-46: 44 needs the index first");
    }

    [Fact]
    public async Task AddTicket_Refused_SaysNothing()
    {
        var tools = await ToolsAsync();
        _edits.Setup(e => e.AddTicketAsync(It.IsAny<SeriesRunRecord>(), "x", "y", It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new SeriesCutAnswer("Refused: a cycle.", false));

        (await tools.AddTicketAsync("x", "y", reason: "why")).Should().Be("Refused: a cycle.");
        _beat.Said.Should().BeEmpty();
        _beat.Facts.Should().HaveCount(3);
    }

    [Fact]
    public async Task LinkTickets_ReachesTheActWithIds_AndSaysIt()
    {
        var tools = await ToolsAsync();
        _edits.Setup(e => e.LinkAsync(It.IsAny<SeriesRunRecord>(), "45", new[] { "44" }, new[] { "43" }, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeriesCutAnswer("SCRUM-45 requires SCRUM-44.", true));

        (await tools.LinkTicketsAsync("SCRUM-45", ["SCRUM-44"], ["43"], "it builds on 44")).Should().Be("SCRUM-45 requires SCRUM-44.");
        _beat.ActDid.Should().Be("re-linked SCRUM-45");
    }

    [Fact]
    public async Task UnknownTicket_OrNoRunsControl_ActsOnNothing()
    {
        var tools = await ToolsAsync();
        (await tools.LinkTicketsAsync("SCRUM-99", ["43"])).Should().Contain("'SCRUM-99' is not a ticket of this series");
        (await tools.LinkTicketsAsync("43", ["SCRUM-99"])).Should().Contain("'SCRUM-99' is not a ticket of this series");
        (await tools.AddTicketAsync("x", "y", requiredBy: ["SCRUM-98"])).Should().Contain("'SCRUM-98' is not a ticket");
        var denied = await new SeriesEditTools(new SeriesBeat(SeriesRecords.Running(mayStartRuns: false), _beat.Facts, SeriesRecords.T),
            _edits.Object, _store).AddTicketAsync("x", "y");
        denied.Should().Contain("runs.control");
        _edits.VerifyNoOtherCalls();
    }
}
