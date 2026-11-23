using AgentSmith.Contracts.Series;
using AgentSmith.Tests.Tickets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-23-31dcc: a ticket's page names the series that holds it — also once the series ended — and a ticket
/// outside every series names none.</summary>
public sealed class TicketSeriesRunLinkTests : IDisposable
{
    private readonly TicketStoreHarness _h = new();

    [Fact]
    public async Task TicketDetail_InAnEndedSeries_LinksTheSeries()
    {
        await _h.AddAsync(TicketStoreHarness.Run("run-1", ticket: "42"), TicketStoreHarness.Run("run-2", ticket: "43"));
        await Store.OpenAsync(new SeriesRunRecord("sr-1", "conv-1", "alpha", "operator", true, SeriesRunStates.Done,
            [new SeriesTicket("42", "#42", "Approval contracts", false, TicketStoreHarness.T)], TicketStoreHarness.T), default);

        var inside = await _h.Detail.ReadAsync("alpha", "42", default);
        var outside = await _h.Detail.ReadAsync("alpha", "43", default);

        inside!.SeriesRun.Should().Be(new AgentSmith.Server.Models.SeriesRunLinkView("sr-1", "Approval contracts"));
        outside!.SeriesRun.Should().BeNull();
    }

    [Fact]
    public async Task ForAsync_ARunningSeries_IsFoundWithItsName()
    {
        await Store.OpenAsync(new SeriesRunRecord("sr-1", "conv-1", "alpha", "operator", true, SeriesRunStates.Running,
            [new SeriesTicket("42", "#42", "Approval contracts", false, TicketStoreHarness.T)], TicketStoreHarness.T), default);
        var link = await _h.Services.GetRequiredService<AgentSmith.Server.Services.Series.TicketSeriesRunLink>().ForAsync("alpha", "42", default);

        link!.Id.Should().Be("sr-1");
    }

    public void Dispose() => _h.Dispose();

    private ISeriesRunStore Store => _h.Services.GetRequiredService<ISeriesRunStore>();
}
