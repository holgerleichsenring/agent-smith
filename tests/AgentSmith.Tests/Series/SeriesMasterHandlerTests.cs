using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-17-0207a: the series master runs only when something needs judgement, continues its thread, keeps
/// its spend, and past its budget judges no more and says so once.</summary>
public sealed class SeriesMasterHandlerTests
{
    private readonly InMemorySeriesRunStore _store = new();
    private readonly Mock<IAgenticLoopRunner> _loop = new();
    private readonly AgentSmithConfig _config = new();

    private SeriesMasterHandler Handler => new(_loop.Object, Mock.Of<IPromptCatalog>(p => p.Get(It.IsAny<string>()) == "body"),
        Mock.Of<ISeriesTicketActs>(), _store, _config, Mock.Of<ISeriesConversation>(), new SeriesProgress(_store, Mock.Of<ISeriesRunLaunch>()),
        new SeriesPause(_store, Mock.Of<ISeriesTicketFacts>(), Mock.Of<ISeriesTicketActs>(), Mock.Of<ISeriesConversation>()));

    private async Task<SeriesBeat> RunAsync(SeriesRunRecord record, params string[] reasons)
    {
        await _store.OpenAsync(record, default);
        var beat = new SeriesBeat(record, [], SeriesRecords.T);
        beat.Reasons.AddRange(reasons);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SeriesBeat, beat);
        await Handler.ExecuteAsync(new SeriesMasterContext(pipeline, new ResolvedProject { Name = "sample" }), default);
        return beat;
    }

    [Fact]
    public async Task SeriesMaster_NothingToJudge_RunsNoLoop()
    {
        await RunAsync(SeriesRecords.Running());

        _loop.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SeriesMaster_ARedTicket_JudgesAndKeepsTheThreadAndTheLine()
    {
        _loop.Setup(l => l.RunAsync(It.IsAny<AgenticLoopRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgenticLoopResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Retried SCRUM-44.")), TimeSpan.Zero));

        var beat = await RunAsync(SeriesRecords.Running(), "SCRUM-44 turned red: red · flaky");

        beat.MasterLine.Should().Be("Retried SCRUM-44.");
        SeriesThread.Read(_store.Records["sr-1"].ThreadJson).Should().HaveCount(2);
        _loop.Verify(l => l.RunAsync(It.Is<AgenticLoopRequest>(r => r.UserPrompt.Contains("SCRUM-44 turned red")
            && r.Tools.Any(t => t.Name == "retry_ticket")), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task SeriesMaster_BudgetSpent_JudgesNoMoreAndSaysSoOnce()
    {
        _config.Dialogue.SeriesMasterBudgetUsd = 5m;
        var spent = SeriesRecords.Running() with { MasterCostUsd = 5m };

        var first = await RunAsync(spent, "SCRUM-44 turned red");
        _store.Records.Clear();
        var second = await RunAsync(spent with { BudgetSaid = true }, "SCRUM-45 turned red");

        _loop.VerifyNoOtherCalls();
        first.Said.Should().ContainSingle().Which.Should().Contain("dialogue.series_master_budget_usd");
        second.Said.Should().BeEmpty();
    }
}
