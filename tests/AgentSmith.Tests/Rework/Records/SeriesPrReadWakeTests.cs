using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>2026-11-25-9992g: a read of a series ticket's pull request that changed what its master judges wakes the series
/// with the change; a read that changed nothing, and a standalone ticket's read, wake nobody.</summary>
public sealed class SeriesPrReadWakeTests : IDisposable
{
    private readonly PrReviewRefreshHarness _h = new();
    private string _ci = CiStates.Green;

    public SeriesPrReadWakeTests()
    {
        _h.Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, CancellationToken _) => new PrReviewState(url.Split('/')[^1], PrReviewStates.Open, PrReviewRefreshHarness.T, [], "abc1234ff"));
        _h.Provider.As<IPullRequestCiReader>().Setup(c => c.ReadCiAsync(It.IsAny<string>(), It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new PullRequestCi(_ci, "abc1234ff", null, []));
        _h.Provider.Setup(p => p.ReadPullRequestBaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("main");
    }

    public void Dispose() => _h.Dispose();

    private Task RefreshAsync() =>
        _h.Refresh().RefreshAsync(PrReviewRefreshHarness.Project, "7", null, CancellationToken.None);

    private Task OpenSeriesAsync() => _h.Series.OpenAsync(Series.SeriesRecords.Running() with
    {
        Project = "p", Tickets = [Series.SeriesRecords.Ticket("7")],
    }, default);

    [Fact]
    public async Task SeriesTicket_CiTurnsGreen_WakesTheSeriesOnceWithTheVerdict()
    {
        await OpenSeriesAsync();
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);

        await RefreshAsync();
        await RefreshAsync(); // the same verdict again changes nothing

        var wakes = (await _h.Series.GetAsync("sr-1", default))!.Wakes;
        wakes.Where(w => w.Kind == SeriesWakeKinds.Ci).Should().ContainSingle()
            .Which.Text.Should().Contain("abc1234").And.Contain("is green");
        wakes.Should().Contain(w => w.Kind == SeriesWakeKinds.PullRequest && w.Text.Contains("is open"));
        wakes.Should().OnlyContain(w => w.TicketId == "7");
    }

    [Fact]
    public async Task SeriesTicket_CiTurnsRed_WakesAgain()
    {
        await OpenSeriesAsync();
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        await RefreshAsync();

        _ci = CiStates.Red;
        await RefreshAsync();

        (await _h.Series.GetAsync("sr-1", default))!.Wakes.Should().Contain(w => w.Kind == SeriesWakeKinds.Ci && w.Text.Contains("is red"));
    }

    [Fact]
    public async Task StandaloneTicket_WakesNobody()
    {
        await _h.Series.OpenAsync(Series.SeriesRecords.Running() with { Project = "p" }, default); // holds 43..45, not 7
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);

        await RefreshAsync();

        (await _h.Series.GetAsync("sr-1", default))!.Wakes.Should().BeEmpty();
    }
}
