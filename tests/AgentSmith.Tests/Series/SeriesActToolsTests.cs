using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Series;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-17-0207a: the series master's acts reach the ticket's own paths as the approver, refuse an unknown
/// ticket and an approver without runs.control, and stop ends the series.</summary>
public sealed class SeriesActToolsTests
{
    private readonly InMemorySeriesRunStore _store = new();
    private readonly Mock<ISeriesTicketActs> _acts = new();

    private SeriesActTools Tools(SeriesRunRecord record) =>
        new(new SeriesBeat(record, [.. record.Tickets.Select(t => new SeriesTicketFact(t))], SeriesRecords.T), _acts.Object, _store);

    [Fact]
    public async Task RetryTicket_KnownTicket_ReachesThePortAsTheApprover()
    {
        _acts.Setup(a => a.RetryAsync("sample", "44", "operator", It.IsAny<CancellationToken>())).ReturnsAsync("retried");

        (await Tools(SeriesRecords.Running()).RetryTicketAsync("SCRUM-44")).Should().Be("retried");
    }

    [Fact]
    public async Task TellTicket_ApproverWithoutRunsControl_ActsOnNoTicket()
    {
        var said = await Tools(SeriesRecords.Running(mayStartRuns: false)).TellTicketAsync("SCRUM-44", "use German");

        said.Should().Contain("runs.control");
        _acts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StartTicket_UnknownTicket_NamesTheSeriesTickets()
    {
        (await Tools(SeriesRecords.Running()).StartTicketAsync("SCRUM-99")).Should().Contain("SCRUM-43, SCRUM-44, SCRUM-45");
    }

    [Fact]
    public async Task StopSeries_EndsTheRecordAndSaysWhy()
    {
        var record = SeriesRecords.Running();
        await _store.OpenAsync(record, default);
        var tools = Tools(record);

        await tools.StopSeriesAsync("the operator asked");

        _store.Records["sr-1"].State.Should().Be(SeriesRunStates.Stopped);
    }
}
