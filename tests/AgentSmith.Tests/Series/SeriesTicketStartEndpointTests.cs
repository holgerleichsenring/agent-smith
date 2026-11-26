using AgentSmith.Contracts.Series;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-26-5f6e: POST /api/series-runs/{id}/tickets/{ticketId}/start — a person's start of a member ticket from
/// the series page, answered as what it came to; a ticket outside the series is 404, a paused series starts nothing.</summary>
public sealed class SeriesTicketStartEndpointTests
{
    private readonly InMemorySeriesRunStore _store = new();
    private readonly Mock<ISeriesTicketActs> _acts = new();

    [Fact]
    public async Task Start_AMemberTicket_AnswersTheStartsOutcome()
    {
        await _store.OpenAsync(SeriesRecords.Running(), default);
        _acts.Setup(a => a.StartAsync("sample", "45", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeriesStart(SeriesStartKind.Started, "Ticket 45 started: run run-7.", "run-7"));

        var result = await SeriesRunEndpoints.StartAsync("sr-1", "45", _store, _acts.Object, default);

        result.Should().BeOfType<Ok<SeriesStartView>>().Which.Value.Should().Be(new SeriesStartView("started", "Ticket 45 started: run run-7.", "run-7"));
    }

    [Fact]
    public async Task Start_ATicketOutsideTheSeries_IsNotFound()
    {
        await _store.OpenAsync(SeriesRecords.Running(), default);

        (await SeriesRunEndpoints.StartAsync("sr-1", "99", _store, _acts.Object, default)).Should().BeOfType<NotFound>();
        _acts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Start_APausedSeries_IsRefusedWithoutAStart()
    {
        await _store.OpenAsync(SeriesRecords.Running() with { State = SeriesRunStates.Paused }, default);

        var result = await SeriesRunEndpoints.StartAsync("sr-1", "45", _store, _acts.Object, default);

        result.Should().BeOfType<Ok<SeriesStartView>>().Which.Value!.Kind.Should().Be("refused");
        _acts.VerifyNoOtherCalls();
    }
}
