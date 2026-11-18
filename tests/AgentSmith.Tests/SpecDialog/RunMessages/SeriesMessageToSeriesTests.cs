using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Series;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.SpecDialog.RunMessages;

/// <summary>2026-11-17-0207b: a message in a Work it out conversation whose filing runs as a series goes to the series
/// master — stamped with the series' id, no design turn — when its poster may start runs; otherwise it routes as
/// before.</summary>
public sealed class SeriesMessageToSeriesTests : IDisposable
{
    private readonly TicketMessageRig _rig = new();

    private async Task<string> OpenSeriesAsync()
    {
        var state = await _rig.Harness.OpenAsync(TicketMessageRig.Platform, TicketMessageRig.Thread);
        await _rig.Series.OpenAsync(SeriesRecords.Running("sr-1", state.JobId), default);
        return state.JobId;
    }

    [Fact]
    public async Task Message_SeriesRunning_IsStampedForTheSeriesAndRunsNoTurn()
    {
        await OpenSeriesAsync();

        await _rig.SendAsync("Keep the card texts in German.");

        _rig.Runner.Verify(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(), It.IsAny<CancellationToken>()), Times.Never);
        (await _rig.TranscriptAsync()).Last(t => t.Role == TranscriptRole.User).SeriesRunId.Should().Be("sr-1");
    }

    [Fact]
    public async Task Message_PosterWithoutRunsControl_RoutesAsBefore()
    {
        await OpenSeriesAsync();

        await _rig.SendAsync("What is happening?", mayStartRuns: false);

        _rig.Runner.Verify(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Message_SeriesStopped_RoutesAsBefore()
    {
        await OpenSeriesAsync();
        await _rig.Series.UpdateAsync("sr-1", r => r with { State = Contracts.Series.SeriesRunStates.Stopped }, default);

        await _rig.SendAsync("Let us plan the next thing.");

        _rig.Runner.Verify(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(), It.IsAny<CancellationToken>()), Times.Once);
        (await _rig.TranscriptAsync()).Should().OnlyContain(t => t.SeriesRunId == null);
    }

    public void Dispose() => _rig.Dispose();
}
