using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.TestSupport;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-9992g: one whole beat — the read, then the master — over an in-memory store, with the model's
/// loop recorded, for the wake and budget tests.</summary>
internal sealed class SeriesWakeBeatRig
{
    public InMemorySeriesRunStore Store { get; } = new();

    public Mock<IAgenticLoopRunner> Loop { get; } = new();

    public AgentSmithConfig Config { get; } = new() { Projects = new() { ["sample"] = new ResolvedProject { Name = "sample" } } };

    public IReadOnlyList<SeriesTicketFact> Facts { get; set; } = [SeriesRecords.Fact("43"), SeriesRecords.Fact("44"), SeriesRecords.Fact("45")];

    public List<string> Prompts { get; } = [];

    public Mock<ISeriesRunLaunch> Launch { get; } = new();

    private readonly Mock<ISeriesConversation> _conversation = new();

    public SeriesWakeBeatRig()
    {
        _conversation.Setup(c => c.AddressedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Loop.Setup(l => l.RunAsync(It.IsAny<AgenticLoopRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AgenticLoopRequest, CancellationToken>((r, _) => Prompts.Add(r.UserPrompt))
            .ReturnsAsync(new AgenticLoopResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Noted.")), TimeSpan.Zero));
    }

    /// <summary>The record as the table last saw it, so only what the test adds makes a beat due.</summary>
    public SeriesRunRecord Seen(SeriesRunRecord record, DateTimeOffset now) =>
        record with { LastTable = SeriesTable.Snapshot(SeriesTable.Rows(Facts, now)), LastStatusAt = now };

    public Task<string?> DueAsync(SeriesRunRecord record, DateTimeOffset now) =>
        new SeriesBeatDemand(_conversation.Object, Config, Launch.Object).WhyAsync(record, Facts, now, default);

    public async Task<SeriesBeat> BeatAsync(SeriesRunRecord record, DateTimeOffset now, params SeriesOperatorMessage[] messages)
    {
        if (!Store.Records.ContainsKey(record.Id)) await Store.OpenAsync(record, default);
        _conversation.Setup(c => c.AddressedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SeriesRunId, record.Id);
        var clock = new Mock<TimeProvider>();
        clock.Setup(c => c.GetUtcNow()).Returns(now);
        var facts = Mock.Of<ISeriesTicketFacts>(f => f.ReadAsync(It.IsAny<SeriesRunRecord>(), It.IsAny<CancellationToken>()) == Task.FromResult(Facts));
        await new ReadSeriesHandler(Store, facts, _conversation.Object, Config, clock.Object,
                new SeriesQuestions(Store, SeriesWakeDoubles.Budget(Store, Config)),
                new SeriesProgress(Store, Launch.Object))
            .ExecuteAsync(new ReadSeriesContext(pipeline, Config.Projects["sample"]), default);
        await Master().ExecuteAsync(new SeriesMasterContext(pipeline, Config.Projects["sample"]), default);
        pipeline.TryGet<SeriesBeat>(ContextKeys.SeriesBeat, out var beat);
        return beat!;
    }

    private SeriesMasterHandler Master() => new(Loop.Object, Mock.Of<IPromptCatalog>(p => p.Get(It.IsAny<string>()) == "body"),
        Mock.Of<ISeriesTicketActs>(), Store, Config, _conversation.Object, new SeriesProgress(Store, Launch.Object),
        new SeriesPause(Store, Mock.Of<ISeriesTicketFacts>(), Mock.Of<ISeriesTicketActs>(), _conversation.Object),
        Mock.Of<ISeriesPullRequestActs>(), SeriesFinalDoubles.Merge(), SeriesFinalDoubles.Ci(), SeriesWakeDoubles.Budget(Store, Config), Mock.Of<ISeriesEditActs>(),
        NullLogger<SeriesMasterHandler>.Instance);
}
