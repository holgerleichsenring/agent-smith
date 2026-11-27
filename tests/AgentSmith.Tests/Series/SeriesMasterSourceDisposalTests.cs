using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-27-3532c: the beat owns its read scopes — they are disposed after a judged beat, a failed loop and a
/// cancelled one, and a failure opening them takes the failed loop's back-off.</summary>
public sealed class SeriesMasterSourceDisposalTests
{
    private readonly InMemorySeriesRunStore _store = new();
    private readonly Mock<IAgenticLoopRunner> _loop = new();
    private readonly Mock<ISeriesSourceScopes> _sources = new();
    private readonly Mock<ISourceScopeSandbox> _scope = new();
    private readonly AgentSmithConfig _config = new();

    public SeriesMasterSourceDisposalTests() =>
        _sources.Setup(s => s.OpenAsync(It.IsAny<SeriesBeat>(), It.IsAny<ResolvedProject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new SeriesSourceMap([new SeriesSource("api", "api at its default branch", _scope.Object)]));

    [Fact]
    public async Task Judge_ALoopThatAnswers_DisposesTheScopes()
    {
        _loop.Setup(l => l.RunAsync(It.IsAny<AgenticLoopRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgenticLoopResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")), TimeSpan.Zero));

        (await JudgeAsync()).IsSuccess.Should().BeTrue();

        _scope.Verify(s => s.DisposeAsync(), Times.Once());
    }

    [Fact]
    public async Task Judge_ALoopThatThrows_DisposesTheScopes()
    {
        _loop.Setup(l => l.RunAsync(It.IsAny<AgenticLoopRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("model down"));

        (await JudgeAsync()).IsSuccess.Should().BeFalse();

        _scope.Verify(s => s.DisposeAsync(), Times.Once());
    }

    [Fact]
    public async Task Judge_ACancelledLoop_DisposesTheScopes()
    {
        _loop.Setup(l => l.RunAsync(It.IsAny<AgenticLoopRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

        await FluentActions.Awaiting(JudgeAsync).Should().ThrowAsync<OperationCanceledException>();

        _scope.Verify(s => s.DisposeAsync(), Times.Once());
    }

    [Fact]
    public async Task Judge_OpeningTheScopesFails_TakesTheBackOff()
    {
        _sources.Setup(s => s.OpenAsync(It.IsAny<SeriesBeat>(), It.IsAny<ResolvedProject>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("store down"));

        (await JudgeAsync()).IsSuccess.Should().BeFalse();

        _store.Records["sr-1"].MasterFailedAt.Should().Be(SeriesRecords.T);
        _loop.Verify(l => l.RunAsync(It.IsAny<AgenticLoopRequest>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    private async Task<CommandResult> JudgeAsync()
    {
        var record = SeriesRecords.Running();
        await _store.OpenAsync(record, default);
        var beat = new SeriesBeat(record, [], SeriesRecords.T);
        beat.Reasons.Add("the operator wrote");
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SeriesBeat, beat);
        return await Handler().ExecuteAsync(new SeriesMasterContext(pipeline, new ResolvedProject { Name = "sample" }), default);
    }

    private SeriesMasterHandler Handler() => new(_loop.Object, Mock.Of<IPromptCatalog>(p => p.Get(It.IsAny<string>()) == "body"),
        Mock.Of<ISeriesTicketActs>(), _store, _config, Mock.Of<ISeriesConversation>(), new SeriesProgress(_store, Mock.Of<ISeriesRunLaunch>()),
        new SeriesPause(_store, Mock.Of<ISeriesTicketFacts>(), Mock.Of<ISeriesTicketActs>(), Mock.Of<ISeriesConversation>()),
        Mock.Of<ISeriesPullRequestActs>(), SeriesFinalDoubles.Merge(), SeriesFinalDoubles.Ci(),
        AgentSmith.Tests.TestSupport.SeriesWakeDoubles.Budget(_store, _config), Mock.Of<ISeriesEditActs>(),
        NullLogger<SeriesMasterHandler>.Instance, sources: _sources.Object);
}
