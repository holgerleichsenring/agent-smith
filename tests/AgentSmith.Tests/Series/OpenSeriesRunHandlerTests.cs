using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Filing;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-17-0207a: a filing of two or more tickets opens a series run; one ticket opens none; a filing
/// seeded with a series id joins that series.</summary>
public sealed class OpenSeriesRunHandlerTests
{
    private readonly InMemorySeriesRunStore _store = new();

    private OpenSeriesRunHandler Handler => new(_store, TimeProvider.System);

    private static PipelineContext Filed(int count, string? series = null)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.FilingRequest, new FilingRequest("conv-1", "operator", [], [], null, MayStartRuns: true));
        pipeline.Set(ContextKeys.FilingReport, new FilingReportSlot
        {
            Report = new FilingReport([.. Enumerable.Range(1, count).Select(i =>
                new FiledTicket($"https://tracker/{i}", $"ticket {i}") { TicketId = $"{i}", Key = $"SCRUM-{i}" })], null),
        });
        if (series is not null) pipeline.Set(ContextKeys.SeriesRunId, series);
        return pipeline;
    }

    private Task<AgentSmith.Domain.Models.CommandResult> RunAsync(PipelineContext pipeline) =>
        Handler.ExecuteAsync(new OpenSeriesRunContext(pipeline, new ResolvedProject { Name = "sample" }), default);

    [Fact]
    public async Task OpenSeriesRun_TwoTicketsFiled_OpensOneSeriesForTheConversation()
    {
        await RunAsync(Filed(2));

        var record = _store.Records.Values.Should().ContainSingle().Subject;
        record.Conversation.Should().Be("conv-1");
        record.Principal.Should().Be("operator");
        record.State.Should().Be(SeriesRunStates.Running);
        record.Tickets.Select(t => t.Key).Should().Equal("SCRUM-1", "SCRUM-2");
    }

    [Fact]
    public async Task OpenSeriesRun_OneTicketFiled_OpensNone()
    {
        var result = await RunAsync(Filed(1));

        _store.Records.Should().BeEmpty();
        result.Message.Should().Contain("single ticket");
    }

    [Fact]
    public async Task OpenSeriesRun_SeededWithASeries_JoinsItAsAFollowUp()
    {
        await _store.OpenAsync(SeriesRecords.Running("sr-1") with { State = SeriesRunStates.Verifying }, default);

        await RunAsync(Filed(1, series: "sr-1"));

        var record = _store.Records["sr-1"];
        record.Tickets.Should().HaveCount(4);
        record.Tickets[^1].FollowUp.Should().BeTrue();
        record.State.Should().Be(SeriesRunStates.Running, "the follow-up is worked before the series checks again");
    }

    [Fact]
    public async Task OpenSeriesRun_JoiningAPausedSeries_KeepsItPaused()
    {
        await _store.OpenAsync(SeriesRecords.Running("sr-1") with { State = SeriesRunStates.Paused, PausedFrom = SeriesRunStates.Verifying }, default);

        await RunAsync(Filed(1, series: "sr-1"));

        var record = _store.Records["sr-1"];
        record.State.Should().Be(SeriesRunStates.Paused, "2026-11-19-1b72a: a follow-up does not lift the operator's pause");
        record.PausedFrom.Should().Be(SeriesRunStates.Running, "on resume the follow-up is worked before the series checks again");
    }
}
