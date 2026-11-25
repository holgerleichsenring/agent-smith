using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-9992e: a delivering series with final pull requests is beaten every merge.ci_poll_seconds so the delivery
/// reads them — a final pull request its master merged ends the series at the next poll — and that beat is no reason to call
/// the master; a series that ended in its beat is judged no more.</summary>
public sealed class SeriesDeliveryBeatTests
{
    private readonly InMemorySeriesRunStore _store = new();
    private readonly Mock<IAgenticLoopRunner> _loop = new();
    private readonly Mock<ISeriesConversation> _conversation = new();
    private readonly AgentSmithConfig _config = new()
    {
        Projects = new() { ["sample"] = new ResolvedProject { Name = "sample", Merge = new MergePolicyConfig { CiPollSeconds = 60 } } },
    };

    public SeriesDeliveryBeatTests() =>
        _conversation.Setup(c => c.AddressedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

    [Fact]
    public async Task ADeliveringSeriesWithFinals_IsDueEveryCiPoll_OthersAreNot()
    {
        var facts = new[] { SeriesRecords.Fact("43", landed: SeriesRecords.T) };
        var delivering = Delivering(facts);
        var demand = new SeriesBeatDemand(_conversation.Object, _config, Mock.Of<ISeriesRunLaunch>());

        (await demand.WhyAsync(delivering, facts, SeriesRecords.T.AddSeconds(30), default)).Should().BeNull();
        (await demand.WhyAsync(delivering, facts, SeriesRecords.T.AddSeconds(60), default)).Should().Be(SeriesBeatDemand.FinalsRead);
        (await demand.WhyAsync(delivering with { Deliveries = [] }, facts, SeriesRecords.T.AddSeconds(60), default))
            .Should().BeNull("no final pull request is open yet");
        (await demand.WhyAsync(delivering with { State = SeriesRunStates.Running }, facts, SeriesRecords.T.AddSeconds(60), default))
            .Should().BeNull();
    }

    [Fact]
    public async Task TheDeliveryBeat_GivesTheMasterNoReason_SoTheModelIsNotCalled()
    {
        var facts = new[] { SeriesRecords.Fact("43", landed: SeriesRecords.T) };
        await _store.OpenAsync(Delivering(facts), default);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SeriesRunId, "sr-1");
        var clock = new Mock<TimeProvider>();
        clock.Setup(c => c.GetUtcNow()).Returns(SeriesRecords.T.AddSeconds(60));
        var factsReader = Mock.Of<ISeriesTicketFacts>(f => f.ReadAsync(It.IsAny<SeriesRunRecord>(), It.IsAny<CancellationToken>())
            == Task.FromResult<IReadOnlyList<SeriesTicketFact>>(facts));
        var read = new ReadSeriesHandler(_store, factsReader, _conversation.Object, _config, clock.Object,
            new SeriesQuestions(_store, Mock.Of<IDialogueAnswerInbox>(), AgentSmith.Tests.TestSupport.SeriesWakeDoubles.Budget(_store, _config)), new SeriesProgress(_store, Mock.Of<ISeriesRunLaunch>()));

        await read.ExecuteAsync(new ReadSeriesContext(pipeline, _config.Projects["sample"]), default);
        await Master().ExecuteAsync(new SeriesMasterContext(pipeline, _config.Projects["sample"]), default);

        pipeline.TryGet<SeriesBeat>(ContextKeys.SeriesBeat, out var beat).Should().BeTrue();
        beat!.Reasons.Should().BeEmpty();
        _loop.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ASeriesThatEndedInItsBeat_IsJudgedNoMore()
    {
        var done = SeriesRecords.Running() with { State = SeriesRunStates.Done };
        await _store.OpenAsync(done, default);
        var beat = new SeriesBeat(done, [], SeriesRecords.T);
        beat.Reasons.Add("Nothing moved for 15 minutes while SCRUM-43 run(s): is one slow or stuck?");
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SeriesBeat, beat);

        await Master().ExecuteAsync(new SeriesMasterContext(pipeline, _config.Projects["sample"]), default);

        _loop.VerifyNoOtherCalls();
    }

    private static SeriesRunRecord Delivering(IReadOnlyList<SeriesTicketFact> facts) => SeriesRecords.Running() with
    {
        State = SeriesRunStates.Delivering, IntegrationBranch = "agent-smith/series/sr-1", LastStatusAt = SeriesRecords.T,
        LastTable = SeriesTable.Snapshot(SeriesTable.Rows(facts, SeriesRecords.T)),
        Deliveries = [new SeriesDelivery("api", "https://github.com/example/api/pull/90", "90")],
        Tickets = [SeriesRecords.Ticket("43")],
    };

    private SeriesMasterHandler Master() => new(_loop.Object, Mock.Of<IPromptCatalog>(p => p.Get(It.IsAny<string>()) == "body"),
        Mock.Of<ISeriesTicketActs>(), _store, _config, _conversation.Object, new SeriesProgress(_store, Mock.Of<ISeriesRunLaunch>()),
        new SeriesPause(_store, Mock.Of<ISeriesTicketFacts>(), Mock.Of<ISeriesTicketActs>(), _conversation.Object),
        Mock.Of<ISeriesPullRequestActs>(), SeriesFinalDoubles.Merge(), SeriesFinalDoubles.Ci(),
        AgentSmith.Tests.TestSupport.SeriesWakeDoubles.Budget(_store, _config), Mock.Of<ISeriesEditActs>(), NullLogger<SeriesMasterHandler>.Instance);
}
