using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-e94eb: no series conversation is posted status — no table, no failed or waits-for-your-merge
/// lines, whatever platform holds it; the series page shows them. A quiet beat still restarts the stall clock.</summary>
public sealed class PostSeriesStatusHandlerTests
{
    private readonly InMemorySeriesRunStore _store = new();
    private readonly Mock<ISeriesConversation> _conversation = new();
    private readonly List<string> _said = [];

    private static readonly ResolvedProject Project = new() { Name = "sample", Merge = new MergePolicyConfig { When = MergeWhen.Manual } };

    private async Task PostAsync(SeriesRunRecord record, params SeriesTicketFact[] facts)
    {
        await _store.OpenAsync(record, default);
        _conversation.Setup(c => c.SayAsync("conv-1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, text, _) => _said.Add(text)).Returns(Task.CompletedTask);
        var beat = new SeriesBeat(record, facts, SeriesRecords.T.AddMinutes(20));
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SeriesBeat, beat);
        await new PostSeriesStatusHandler(_store, _conversation.Object)
            .ExecuteAsync(new PostSeriesStatusContext(pipeline, Project), default);
    }

    private static SeriesRunRecord Reported(string table) =>
        SeriesRecords.Running() with { LastStatusAt = SeriesRecords.T, LastTable = table };

    [Fact]
    public async Task PostSeriesStatus_FailedAndMergeWaiting_PostsNothing_AndRestartsTheStallClock()
    {
        var record = Reported("SCRUM-43=coding\nSCRUM-44=waits on SCRUM-43\nSCRUM-45=not started");

        await PostAsync(record, SeriesRecords.Fact("43", RunStatuses.Failed) with { EndDetail = "tests failed" },
            SeriesRecords.Fact("44", requires: "43"), SeriesRecords.Fact("45", RunStatuses.Success));

        _said.Should().BeEmpty("the series page lists what failed and what waits for a merge");
        _store.Records["sr-1"].LastStatusAt.Should().Be(SeriesRecords.T.AddMinutes(20));
        _store.Records["sr-1"].LastTable.Should().Contain("SCRUM-43=");
    }

    [Fact]
    public async Task PostSeriesStatus_FirstBeat_PostsOnlyTheOpening()
    {
        await PostAsync(SeriesRecords.Running(), SeriesRecords.Fact("43", RunStatuses.Running, finished: false));

        _said.Should().ContainSingle().Which.Should().StartWith("Filed 1 tickets. SCRUM-43 started").And.NotContain("Status at");
    }
}
