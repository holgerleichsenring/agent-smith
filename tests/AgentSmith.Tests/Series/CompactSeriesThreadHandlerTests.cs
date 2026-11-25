using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-433fb: past dialogue.series_master_thread_max_chars the master's older beats become its notes and the
/// latest beats stay; under it, on a failed or empty note and with the budget spent, the thread is kept byte for byte.</summary>
public sealed class CompactSeriesThreadHandlerTests
{
    private readonly InMemorySeriesRunStore _store = new();
    private readonly Mock<IAgenticLoopRunner> _loop = new();
    private readonly AgentSmithConfig _config = new() { Dialogue = { SeriesMasterThreadMaxChars = 1000, SeriesMasterThreadKeepTurns = 2 } };

    private static string Thread(int beats) => Enumerable.Range(1, beats).Aggregate("[]",
        (json, i) => SeriesThread.Append(json, $"beat {i}: " + new string('x', 200), $"answer {i}"));

    private async Task<(CommandResult Result, SeriesRunRecord Kept)> RunAsync(SeriesRunRecord record, bool judged = true)
    {
        await _store.OpenAsync(record, default);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SeriesBeat, new SeriesBeat(record, [], SeriesRecords.T) { MasterJudged = judged });
        var result = await new CompactSeriesThreadHandler(_loop.Object, _store, _config, SeriesWakeDoubles.Budget(_store, _config),
            NullLogger<CompactSeriesThreadHandler>.Instance)
            .ExecuteAsync(new CompactSeriesThreadContext(pipeline, new ResolvedProject { Name = "sample" }), default);
        return (result, _store.Records[record.Id]);
    }

    private void Notes(string text) => _loop.Setup(l => l.RunAsync(It.IsAny<AgenticLoopRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new AgenticLoopResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)), TimeSpan.Zero));

    [Fact]
    public async Task Compact_OverTheBound_OlderBeatsBecomeTheNotes_TheLatestStay()
    {
        Notes("Decided: #4 before #7, because the api lands first.");

        var (result, kept) = await RunAsync(SeriesRecords.Running() with { ThreadJson = Thread(6) });

        SeriesThread.Read(kept.ThreadJson).Select(m => m.Text).Should().Equal(SeriesThread.NotesHead,
            "Decided: #4 before #7, because the api lands first.", "beat 5: " + new string('x', 200), "answer 5",
            "beat 6: " + new string('x', 200), "answer 6");
        result.Message.Should().Contain("dialogue.series_master_thread_max_chars (1000)").And.Contain("its 4 older entries")
            .And.Contain("dialogue.series_master_thread_keep_turns");
        _loop.Verify(l => l.RunAsync(It.Is<AgenticLoopRequest>(r => r.Tools.Count == 0 && r.PriorMessages!.Count == 8
            && r.UserPrompt == CompactSeriesThreadHandler.Instruction), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Compact_UnderTheBound_KeepsTheThreadAndCallsNoModel()
    {
        var (_, kept) = await RunAsync(SeriesRecords.Running() with { ThreadJson = Thread(2) });

        kept.ThreadJson.Should().Be(Thread(2));
        _loop.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Compact_ABeatTheMasterDidNotJudge_CallsNoModel()
    {
        var (_, kept) = await RunAsync(SeriesRecords.Running() with { ThreadJson = Thread(6) }, judged: false);

        kept.ThreadJson.Should().Be(Thread(6), "a failed note is tried again at the next judged beat, not at every beat");
        _loop.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Compact_OnlyTheLatestBeatsAreLarge_FoldsNothing()
    {
        var thread = SeriesThread.Append(SeriesThread.Append(SeriesThread.Append("[]", "old", "a"), "big", new string('y', 1200)), "big", "b");

        var (_, kept) = await RunAsync(SeriesRecords.Running() with { ThreadJson = thread });

        kept.ThreadJson.Should().Be(thread, "an older part under half the bound is not worth a model call every beat");
        _loop.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Compact_TheNotesFail_KeepsTheThreadByteForByte()
    {
        _loop.Setup(l => l.RunAsync(It.IsAny<AgenticLoopRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("down"));

        var (result, kept) = await RunAsync(SeriesRecords.Running() with { ThreadJson = Thread(6) });

        kept.ThreadJson.Should().Be(Thread(6));
        result.IsSuccess.Should().BeTrue("a beat whose notes failed is not a failed beat; a later one compacts");
    }

    [Fact]
    public async Task Compact_TheBudgetIsSpent_KeepsTheThreadAndCallsNoModel()
    {
        _config.Dialogue.SeriesMasterBudgetUsd = 5m;

        var (_, kept) = await RunAsync(SeriesRecords.Running() with { ThreadJson = Thread(6), MasterCostUsd = 5m });

        kept.ThreadJson.Should().Be(Thread(6));
        _loop.VerifyNoOtherCalls();
    }

    [Fact]
    public void Kept_TheStoredTurn_CarriesNoTable()
    {
        var beat = new SeriesBeat(SeriesRecords.Running(), [SeriesRecords.Fact("43")], SeriesRecords.T);
        beat.Reasons.Add("SCRUM-43 turned red");

        SeriesMasterPrompt.Build(beat).Should().Contain("| ticket |");
        SeriesMasterPrompt.Kept(beat).Should().NotContain("| ticket |").And.Contain("SCRUM-43 turned red");
    }
}
