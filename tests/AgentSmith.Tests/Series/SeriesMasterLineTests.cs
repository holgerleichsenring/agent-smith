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

/// <summary>2026-11-25-e94ec: the master's reply reaches the conversation only when it acted, its question was answered
/// or the operator wrote to it — read from its tool calls, never from its words. A beat in which it only looked keeps
/// the reply in its thread.</summary>
public sealed class SeriesMasterLineTests
{
    private readonly InMemorySeriesRunStore _store = new();
    private readonly Mock<IAgenticLoopRunner> _loop = new();
    private readonly Mock<ISeriesConversation> _conversation = new();
    private static readonly ResolvedProject Project = new() { Name = "sample" };

    private SeriesMasterHandler Handler => new(_loop.Object, Mock.Of<IPromptCatalog>(p => p.Get(It.IsAny<string>()) == "body"),
        Mock.Of<ISeriesTicketActs>(), _store, new AgentSmithConfig(), _conversation.Object,
        new SeriesProgress(_store, Mock.Of<ISeriesRunLaunch>()),
        new SeriesPause(_store, Mock.Of<ISeriesTicketFacts>(), Mock.Of<ISeriesTicketActs>(), Mock.Of<ISeriesConversation>()),
        Mock.Of<ISeriesPullRequestActs>(), SeriesFinalDoubles.Merge(), SeriesFinalDoubles.Ci(),
        Microsoft.Extensions.Logging.Abstractions.NullLogger<SeriesMasterHandler>.Instance);

    /// <summary>A beat whose master calls the named tool once, then answers.</summary>
    private async Task<SeriesBeat> JudgeAsync(string? tool, string arguments = "{}")
    {
        _conversation.Setup(c => c.AddressedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _loop.Setup(l => l.RunAsync(It.IsAny<AgenticLoopRequest>(), It.IsAny<CancellationToken>()))
            .Returns<AgenticLoopRequest, CancellationToken>(async (request, ct) =>
            {
                if (tool is not null)
                    await request.Tools.OfType<AIFunction>().Single(t => t.Name == tool)
                        .InvokeAsync(new AIFunctionArguments(System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments)), ct);
                return new AgenticLoopResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "SCRUM-43 is still running, not stuck.")), TimeSpan.Zero);
            });
        await _store.OpenAsync(SeriesRecords.Running() with { LastStatusAt = SeriesRecords.T }, default);
        var beat = new SeriesBeat(_store.Records["sr-1"], [SeriesRecords.Fact("43", "running", finished: false)], SeriesRecords.T);
        beat.Reasons.Add("Nothing moved for 15 minutes while SCRUM-43 run(s): is one slow or stuck?");
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SeriesBeat, beat);
        await Handler.ExecuteAsync(new SeriesMasterContext(pipeline, Project), default);
        return beat;
    }

    [Fact]
    public async Task MasterOnlyLooked_ReplyStaysInItsThread_NotInTheConversation()
    {
        var beat = await JudgeAsync("read_ticket", """{"ticket":"SCRUM-43"}""");

        beat.MasterActed.Should().BeFalse();
        SeriesReport.Messages(beat, Project).Should().BeEmpty();
        SeriesThread.Read(_store.Records["sr-1"].ThreadJson).Last().Text.Should().Be("SCRUM-43 is still running, not stuck.");
    }

    [Fact]
    public async Task MasterActed_ReplyIsPosted()
    {
        var beat = await JudgeAsync("retry_ticket", """{"ticket":"SCRUM-43"}""");

        beat.MasterActed.Should().BeTrue();
        SeriesReport.Messages(beat, Project).Should().Equal("SCRUM-43 is still running, not stuck.");
    }

    [Fact]
    public async Task OperatorWroteMidTurn_ReplyIsPosted()
    {
        _conversation.SetupSequence(c => c.AddressedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new SeriesOperatorMessage(30, "how is SCRUM-43?", SeriesRecords.T)]);
        _loop.Setup(l => l.RunAsync(It.IsAny<AgenticLoopRequest>(), It.IsAny<CancellationToken>()))
            .Returns<AgenticLoopRequest, CancellationToken>(async (request, ct) =>
            {
                await request.Tools.OfType<AIFunction>().Single(t => t.Name == "series_status").InvokeAsync(new AIFunctionArguments(), ct);
                return new AgenticLoopResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "SCRUM-43 is coding.")), TimeSpan.Zero);
            });
        await _store.OpenAsync(SeriesRecords.Running() with { LastStatusAt = SeriesRecords.T }, default);
        var beat = new SeriesBeat(_store.Records["sr-1"], [], SeriesRecords.T);
        beat.Reasons.Add("a reason");
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SeriesBeat, beat);

        await Handler.ExecuteAsync(new SeriesMasterContext(pipeline, Project), default);

        beat.MessagesThrough.Should().Be(30);
        SeriesReport.Messages(beat, Project).Should().Equal("SCRUM-43 is coding.");
    }

    [Fact]
    public void Report_MasterLine_PostedForAnOperatorMessageOrItsAnsweredQuestion_NotOtherwise()
    {
        SeriesBeat Beat()
        {
            var beat = new SeriesBeat(SeriesRecords.Running() with { LastStatusAt = SeriesRecords.T }, [], SeriesRecords.T);
            beat.MasterLine = "Going with keep.";
            return beat;
        }

        SeriesReport.Messages(Beat(), Project).Should().BeEmpty();
        var wrote = Beat();
        wrote.Messages.Add(new SeriesOperatorMessage(3, "what now?", SeriesRecords.T));
        SeriesReport.Messages(wrote, Project).Should().Equal("Going with keep.");
        var answered = Beat();
        answered.MasterAnswered = true;
        SeriesReport.Messages(answered, Project).Should().Equal("Going with keep.");
    }

    [Fact]
    public async Task MastersOwnQuestion_AnsweredByDefault_MarksTheBeatAnswered()
    {
        await _store.OpenAsync(SeriesRecords.Running() with
        {
            Question = SeriesQuestions.Ask("Keep it?", ["keep", "drop"], 0, SeriesRecords.T, 120),
        }, default);
        var beat = new SeriesBeat(_store.Records["sr-1"], [], SeriesRecords.T.AddMinutes(121));

        await new SeriesQuestions(_store, Mock.Of<IDialogueAnswerInbox>()).SettleAsync(beat, 120, default);

        beat.MasterAnswered.Should().BeTrue();
    }
}
