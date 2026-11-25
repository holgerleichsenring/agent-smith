using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-433fa: a beat tells the pages of the members it saw change — all of them when it moved the series'
/// state — through the one ticket nudge, which the series page re-reads on; a beat that changed nothing nudges none.</summary>
public sealed class SeriesTicketNudgesTests
{
    private static readonly SeriesTicketFact[] Before =
        [SeriesRecords.Fact("43", RunStatuses.Running, finished: false), SeriesRecords.Fact("44"), SeriesRecords.Fact("45")];

    private static SeriesRunRecord Seen(string state = SeriesRunStates.Running) =>
        SeriesRecords.Running() with { State = state, LastTable = SeriesTable.Snapshot(SeriesTable.Rows(Before, SeriesRecords.T)) };

    private static async Task<List<string>> SendAsync(SeriesBeat beat)
    {
        var keys = new List<string>();
        var activity = new Mock<ITicketActivityNudge>();
        activity.Setup(a => a.ChangedAsync("sample", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, key, _) => keys.Add(key)).Returns(Task.CompletedTask);
        await new SeriesTicketNudges(activity.Object).SendAsync(beat, new ResolvedProject { Name = "sample" }, default);
        return keys;
    }

    [Fact]
    public async Task Send_OneTicketEnded_NudgesOnlyIt()
    {
        var beat = new SeriesBeat(Seen(), [SeriesRecords.Fact("43", RunStatuses.Failed), Before[1], Before[2]], SeriesRecords.T);

        (await SendAsync(beat)).Should().Equal("github-43");
    }

    [Fact]
    public async Task Send_NothingChanged_NudgesNone()
    {
        (await SendAsync(new SeriesBeat(Seen(), Before, SeriesRecords.T))).Should().BeEmpty();
    }

    [Fact]
    public async Task Send_FirstBeat_NudgesEveryMember_ItSawNoTableBefore()
    {
        var beat = new SeriesBeat(SeriesRecords.Running(), Before, SeriesRecords.T);

        (await SendAsync(beat)).Should().HaveCount(3);
    }

    [Fact]
    public async Task Send_TheBeatMovedTheSeriesState_NudgesEveryMember()
    {
        var beat = new SeriesBeat(Seen(), Before, SeriesRecords.T);
        beat.Record = beat.Record with { State = SeriesRunStates.Verifying };

        (await SendAsync(beat)).Should().Equal("github-43", "github-44", "github-45");
    }
}
