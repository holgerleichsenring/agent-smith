using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-19-1b72a: a dashboard conversation has the Series pane, so the series posts it no table — only what
/// needs the operator; a chat platform has no pane and keeps the table.</summary>
public sealed class PostSeriesStatusHandlerTests
{
    private readonly InMemorySeriesRunStore _store = new();
    private readonly Mock<ISeriesConversation> _conversation = new();
    private readonly List<string> _said = [];

    private static readonly ResolvedProject Project = new() { Name = "sample", Merge = new MergePolicyConfig { When = MergeWhen.Manual } };

    private async Task<SeriesBeat> PostAsync(bool pane, SeriesRunRecord record, params SeriesTicketFact[] facts)
    {
        await _store.OpenAsync(record, default);
        _conversation.Setup(c => c.HasPaneAsync("conv-1", It.IsAny<CancellationToken>())).ReturnsAsync(pane);
        _conversation.Setup(c => c.SayAsync("conv-1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, text, _) => _said.Add(text)).Returns(Task.CompletedTask);
        var beat = new SeriesBeat(record, facts, SeriesRecords.T.AddMinutes(20));
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SeriesBeat, beat);
        await new PostSeriesStatusHandler(_store, _conversation.Object, new AgentSmithConfig())
            .ExecuteAsync(new PostSeriesStatusContext(pipeline, Project), default);
        return beat;
    }

    private static SeriesRunRecord Reported(string table) =>
        SeriesRecords.Running() with { LastStatusAt = SeriesRecords.T, LastTable = table };

    [Fact]
    public async Task PostSeriesStatus_Dashboard_PostsNoTable()
    {
        var record = Reported("SCRUM-43=coding\nSCRUM-44=waits on SCRUM-43\nSCRUM-45=not started");

        await PostAsync(true, record, SeriesRecords.Fact("43", RunStatuses.Failed) with { EndDetail = "tests failed" },
            SeriesRecords.Fact("44", requires: "43"), SeriesRecords.Fact("45", RunStatuses.Success));

        _said.Should().Equal("SCRUM-43 failed: tests failed.", "SCRUM-45's pull request waits for your merge.");
        _store.Records["sr-1"].LastStatusAt.Should().Be(SeriesRecords.T.AddMinutes(20), "a quiet beat still counts as reported");
    }

    [Fact]
    public async Task PostSeriesStatus_Dashboard_NothingNeedsTheOperator_PostsNothing()
    {
        await PostAsync(true, Reported("SCRUM-43=coding"), SeriesRecords.Fact("43", RunStatuses.Running, finished: false));

        _said.Should().BeEmpty();
    }

    [Fact]
    public async Task PostSeriesStatus_ChatPlatform_KeepsTheTable()
    {
        await PostAsync(false, Reported("SCRUM-43=coding"), SeriesRecords.Fact("43", RunStatuses.Running, finished: false));

        _said.Should().ContainSingle().Which.Should().StartWith("Status at 18:57 UTC.");
    }
}
