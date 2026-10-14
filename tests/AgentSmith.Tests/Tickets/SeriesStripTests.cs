using AgentSmith.Server.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using static AgentSmith.Tests.Tickets.TicketSeriesFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>2026-10-13-9a59e: a list row carries its series strip, read for the page in one pass.</summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class SeriesStripTests : IDisposable
{
    private const string R1 = "2026-10-13T09-00-00-0001";
    private const string R2 = "2026-10-13T10-00-00-0002";
    private const string R3 = "2026-10-13T11-00-00-0003";
    private const string A = "2026-10-13-1234a";
    private const string B = "2026-10-13-1234b";
    private const string C = "2026-10-13-1234c";
    private readonly TicketStoreHarness _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task SeriesStrip_Page_OneQuery_CellsForDoneCarriedLiveAskedFailedNotStarted()
    {
        await _store.AddAsync(
            Run(R1, status: "failed"), Step(R1), Cut(R1, 3, Entry(A, 1), Entry(B, 2)), SpecRow(R1, A, "done"),
            Run(R2, status: "running"), Step(R2), Cut(R2, 4, Entry(A, 1), Entry(B, 2), Entry(C, 3)),
            Attempt(R2, 2, "comment on ticket", A), SpecRow(R2, B, "in_progress"), SpecRow(R2, C, "not_started"),
            Run(R3, ticket: "43", status: "failed"), Step(R3), Cut(R3, 2, Entry(A, 1), Entry(B, 2)),
            SpecRow(R3, A, "done"), SpecRow(R3, B, "failed"));

        var rows = (await _store.List.ReadAsync(new TicketListRequest(null, null, null, null), CancellationToken.None)).Tickets;

        Cells(rows.Single(r => r.TicketId == "42")).Should().Equal(("cut", A, "carried"), ("cut", B, "live"), ("cut", C, "not_started"));
        Cells(rows.Single(r => r.TicketId == "43")).Should().Equal(("cut", A, "done"), ("cut", B, "failed"));
    }

    [Fact]
    public async Task SeriesStrip_WaitingRunWithSpecInProgress_IsAsked()
    {
        await _store.AddAsync(Run(R1, status: "waiting_for_input"), Step(R1), Checkpoint(R1),
            Cut(R1, 3, Entry(A, 1), Entry(B, 2)), SpecRow(R1, A, "in_progress"), SpecRow(R1, B, "not_started"));

        var row = (await _store.List.ReadAsync(new TicketListRequest(null, null, null, null), CancellationToken.None)).Tickets.Single();

        Cells(row).Should().Equal(("cut", A, "asked"), ("cut", B, "not_started"));
    }

    [Fact]
    public async Task SeriesStrip_HandbackAfterCut_KeepsTheCellsAndSaysHandedBack()
    {
        await _store.AddAsync(Run(R1, status: "failed"), Step(R1), Cut(R1, 3, Entry(A, 1)), SpecRow(R1, A, "failed"),
            Run(R2, status: "failed"), Step(R2), Handback(R2, 5, "RequirementsContradictRepository"));

        var strip = (await _store.List.ReadAsync(new TicketListRequest(null, null, null, null), CancellationToken.None))
            .Tickets.Single().SeriesStrip!;

        strip.Derive.Should().Be("handed_back");
        strip.Cells.Should().Equal(new TicketSeriesCellView(A, "failed"));
    }

    private static List<(string, string, string)> Cells(TicketSummaryView row) =>
        [.. row.SeriesStrip!.Cells.Select(c => (row.SeriesStrip.Derive, c.SpecId, c.State))];
}
