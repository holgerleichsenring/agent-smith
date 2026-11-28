using System.Text.Json;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.Lifecycle;
using AgentSmith.Server.Services.Series;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-27-3532b: a series ticket's run asks its series master, never the operator — through the REAL server
/// composition. The parked run's question wakes the master as a reason and is not copied into the chat; tell_ticket answers
/// it through the inbox and the resume sweep resumes the run with that answer; the master may carry it to the operator with
/// ask_human first and answer once they reply; the dashboard's answer endpoint refuses a series-led run. The run row and its
/// checkpoint are seeded as a park leaves them; the tickets' facts, the clock and the tracker are the rig's.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SeriesRunQuestionCompositionTests
{
    private const string Project = "fixture-file";
    private const string RunId = "20261127-120000-code-1";
    private const string QuestionId = "ask-1";
    private const string Asked = "Should the refresh window be 5 or 15 minutes?";
    private static readonly DateTimeOffset Noon = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ParkedRun_WakesMasterWithItsQuestion()
    {
        await using var run = await SeriesParkAsync();
        IReadOnlyList<ChatMessage>? prompt = null;
        run.Harness.ChatClient.EnqueueDeferred(() =>
        {
            prompt = run.Harness.ChatClient.LastScriptedMessages;
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "I will ask the operator."));
        });

        await run.BeatAsync();

        string.Join("\n", prompt!.Select(m => m.Text)).Should().Contain($"#1's run is parked on a question to you (target; question id {QuestionId}): \"{Asked}\"");
        (await run.TextsAsync()).Should().NotContain(t => t.Contains(Asked), "the run asks its master, not the chat");
        var kept = await run.RecordAsync();
        kept.Question.Should().BeNull();
        kept.Raised.Should().Contain($"question:{RunId}:{QuestionId}", "the master judged it once");
    }

    [Fact]
    public async Task TellTicket_ParkedRun_AnswerResumesTheRun()
    {
        await using var run = await SeriesParkAsync();
        run.Harness.ChatClient
            .EnqueueToolCall("tell_ticket", """{"ticket":"1","text":"15 minutes","reason":"the session must survive a refresh"}""")
            .EnqueueText("I answered #1: 15 minutes.");

        await run.BeatAsync();

        await run.ResumedWithAsync("15 minutes");
    }

    [Fact]
    public async Task MasterAsksOperator_ReplyThenTellTicket_ResumesTheRun()
    {
        await using var run = await SeriesParkAsync();
        run.Harness.ChatClient
            .EnqueueToolCall("ask_human", """{"question":"How long may a session idle before a refresh?","choices":[{"label":"5 minutes"},{"label":"15 minutes"}],"recommended_index":1,"forTicket":"1"}""")
            .EnqueueText("I asked the operator for #1.");
        await run.BeatAsync();
        (await run.RecordAsync()).Question!.ForTicket.Should().Be("#1");
        (await run.TextsAsync()).Should().Contain(t => t.Contains("How long may a session idle before a refresh?"));

        await run.Services.GetRequiredService<DashboardDialogDispatcher>().DispatchAsync(
            Thread, "15 minutes, it must survive one refresh", "sample.user", true, Project, default);
        IReadOnlyList<ChatMessage>? prompt = null;
        run.Harness.ChatClient
            .EnqueueDeferred(() =>
            {
                prompt = run.Harness.ChatClient.LastScriptedMessages;
                return ToolCall("tell_ticket", """{"ticket":"1","text":"15 minutes","reason":"the operator answered"}""");
            })
            .EnqueueText("I passed the operator's answer to #1.");
        await run.BeatAsync();

        string.Join("\n", prompt!.Select(m => m.Text)).Should()
            .Contain($"(for #1's run {RunId}, question id {QuestionId}) is answered: 15 minutes, it must survive one refresh");
        (await run.RecordAsync()).Question!.Delivered.Should().BeTrue("the master judged the answer it was handed");
        await run.ResumedWithAsync("15 minutes");
    }

    // 2026-11-28-b1db: the night loop. A run's environment question the master carries to the operator with a recommendation
    // is never taken by default: hours past the configured minutes nothing is answered, no beat is due on it, the run's inbox
    // stays empty (the run stays parked and is told nothing), and the master is not handed a default to relay or asked again.
    [Fact]
    public async Task EnvironmentQuestion_CarriedToTheOperator_PastTheDeadline_IsNotTakenByDefault_TheRunStaysParked()
    {
        await using var run = await SeriesParkAsync("environment");
        run.Harness.ChatClient
            .EnqueueToolCall("ask_human", """{"question":"Provision a disposable database and set its connection string for #1?","choices":[{"label":"Provision it"},{"label":"Skip the check"}],"recommended_index":0,"forTicket":"1"}""")
            .EnqueueText("I asked the operator for #1.");
        await run.BeatAsync();
        var asked = (await run.RecordAsync()).Question!;
        asked.Should().Match<SeriesQuestion>(q => q.NoDefault && q.DeadlineAt == null && q.Recommended == "Provision it");
        (await run.TextsAsync()).Should().Contain(t => t.Contains("nothing is taken by default"));

        run.Rig.Clock.Advance(TimeSpan.FromHours(7));
        var record = await run.RecordAsync();
        var facts = await run.Services.GetRequiredService<ISeriesTicketFacts>().ReadAsync(record, default);
        SeriesBeatDue.Why(record with { LastTable = SeriesTable.Snapshot(
            SeriesTable.Rows(facts, run.Rig.Clock.GetUtcNow())) }, facts, run.Rig.Clock.GetUtcNow())
            .Should().BeNull("no deadline makes a beat due");
        var called = false;
        run.Harness.ChatClient.EnqueueDeferred(() =>
        {
            called = true;
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "Still waiting for the operator."));
        });
        await run.BeatAsync();

        var kept = (await run.RecordAsync()).Question!;
        kept.Should().Match<SeriesQuestion>(q => q.Id == asked.Id && q.Answer == null && !q.ByDefault);
        called.Should().BeFalse("no default is handed to the master and the carried question is not raised again: it takes no turn");
        (await run.Services.GetRequiredService<IDialogueAnswerInbox>().GetAsync(RunId, QuestionId, default)).Should().BeNull();
        (await run.Services.GetRequiredService<IRunCheckpointStore>().GetByRunIdAsync(RunId, default))!.ResumedAt.Should().BeNull();
        (await run.TextsAsync()).Should().NotContain(t => t.StartsWith("Taken by default"));
    }

    // A run retried and parked on another question is never answered with what was meant for the earlier one.
    [Fact]
    public async Task TellTicket_AnotherQuestionId_AnswersNothing_AndNamesTheCurrentQuestion()
    {
        await using var run = await SeriesParkAsync();
        IReadOnlyList<ChatMessage>? after = null;
        run.Harness.ChatClient
            .EnqueueToolCall("tell_ticket", """{"ticket":"1","text":"15 minutes","questionId":"ask-0","reason":"the operator answered"}""")
            .EnqueueDeferred(() =>
            {
                after = run.Harness.ChatClient.LastScriptedMessages;
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, "It asks something else now."));
            });

        await run.BeatAsync();

        (await run.Services.GetRequiredService<IDialogueAnswerInbox>().GetAsync(RunId, QuestionId, default)).Should().BeNull();
        string.Join("\n", after!.SelectMany(m => m.Contents).Select(c => c is FunctionResultContent r ? r.Result?.ToString() : null))
            .Should().Contain($"no longer waits on question ask-0; it waits on question {QuestionId}").And.Contain(Asked);
    }

    [Fact]
    public async Task Answer_SeriesLedRun_Refused()
    {
        await using var run = await SeriesParkAsync();
        var transport = new Mock<IDialogueTransport>();

        var result = await AgentSmith.Server.Extensions.RunControlEndpoints.AnswerAsync(RunId, new AgentSmith.Server.Extensions.RunControlEndpoints.AnswerRequest("15 minutes"),
            run.Services.GetRequiredService<IRunCheckpointStore>(), transport.Object,
            run.Services.GetRequiredService<SeriesLedRun>(), default);

        result.Should().BeOfType<Conflict<string>>().Which.Value.Should().Contain($"series {run.Series}");
        transport.VerifyNoOtherCalls();
        // 3532e: the run's own read names the series, so its page shows the question read-only
        var checkpoint = await run.Services.GetRequiredService<IRunCheckpointStore>().GetByRunIdAsync(RunId, default);
        (await run.Services.GetRequiredService<SeriesLedRun>().QuestionOfAsync(checkpoint, default))!.SeriesId.Should().Be(run.Series);
        (await run.Services.GetRequiredService<IDialogueAnswerInbox>().GetAsync(RunId, QuestionId, default)).Should().BeNull();
    }

    private const string Thread = "d-ask";

    // A filed series of three whose first ticket's run parked on a question, its run row and checkpoint as the park leaves them.
    private static async Task<ParkedSeries> SeriesParkAsync(string answerer = "target")
    {
        var rig = new SeriesRig(Noon);
        var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Filing), SandboxBackend.Stub, session: null, SkillsBackend.Fixture, rig.Bind);
        var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var state = await sp.GetRequiredService<SpecDialogSessionManager>().OpenAsync(
            "dashboard", Thread, Thread, "sample.user", new ActiveScope { Project = Project }, default);
        await sp.GetRequiredService<IFilingRun>().RunAsync(state, new TicketGraphOutcome(
        [
            FilePipelineCompositionTests.Ticket("api", []),
            FilePipelineCompositionTests.Ticket("ui", ["api"]),
            FilePipelineCompositionTests.Ticket("docs", ["api"]),
        ]), true, "prop-1");
        var series = (await sp.GetRequiredService<ISeriesRunStore>().ActiveForConversationAsync(state.JobId, default))!.Id;
        var question = new DialogQuestion(QuestionId, QuestionType.FreeText, Asked, null, null, "5 minutes", TimeSpan.FromDays(3))
        {
            Answerers = [answerer],
        };
        await ParkAsync(sp, question);
        rig.Facts.Park("1", RunId, question);
        return new ParkedSeries(harness, scope, series, rig);
    }

    private static async Task ParkAsync(IServiceProvider sp, DialogQuestion question)
    {
        var uow = sp.GetRequiredService<IUnitOfWork>();
        uow.Set<Run>().Add(new Run
        {
            Id = RunId, Project = Project, Pipeline = PipelinePresets.CodeName, TicketId = "1",
            Status = RunStatuses.WaitingForInput, StartedAt = Noon,
        });
        await uow.SaveChangesAsync(default);
        await sp.GetRequiredService<IRunCheckpointStore>().SaveAsync(new RunCheckpointRecord(RunId, Project, "1", null,
            PipelinePresets.CodeName, RunId, QuestionId, JsonSerializer.Serialize(question),
            """[{"Name":"CheckoutSourceCommand"},{"Name":"AgenticMasterCommand"}]""", "[]", 3, Noon, Noon.AddDays(3), null), default);
    }

    private static ChatResponse ToolCall(string tool, string arguments) =>
        new(new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent($"call_{tool}", tool, JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments))]));

    private sealed class ParkedSeries(RealCompositionHarness harness, AsyncServiceScope scope, string series, SeriesRig rig) : IAsyncDisposable
    {
        public RealCompositionHarness Harness => harness;
        public SeriesRig Rig => rig;
        public IServiceProvider Services => scope.ServiceProvider;
        public string Series => series;

        public async Task BeatAsync()
        {
            var beat = await Services.GetRequiredService<IActRun>().RunAsync(Project, PipelinePresets.SeriesRunName,
                new Dictionary<string, object> { [ContextKeys.SeriesRunId] = series });
            beat.IsSuccess.Should().BeTrue(beat.Message);
        }

        public async Task<SeriesRunRecord> RecordAsync() => (await Services.GetRequiredService<ISeriesRunStore>().GetAsync(series, default))!;

        public async Task<IReadOnlyList<string>> TextsAsync() =>
            [.. (await Services.GetRequiredService<SpecDialogSessionManager>().GetOpenByThreadAsync("dashboard", Thread, default))!
                .Transcript.Where(t => t.Role == TranscriptRole.Assistant).Select(t => t.Text)];

        // The answer reached the parked run's inbox; the resume sweep resumes the run with it, carried in the resume payload.
        public async Task ResumedWithAsync(string answer)
        {
            (await Services.GetRequiredService<IDialogueAnswerInbox>().GetAsync(RunId, QuestionId, default))!.Answer.Should().Be(answer);
            (await Services.GetRequiredService<DialogueResumeSweeper>().ScanOnceAsync(default)).Should().Be(1, "the answered checkpoint resumes");
            (await Services.GetRequiredService<IRunCheckpointStore>().GetByRunIdAsync(RunId, default))!.ResumedAt.Should().NotBeNull();
            var queued = Services.GetRequiredService<IUnitOfWork>().Set<QueuedTicket>().Single(q => q.ReservedRunId == RunId);
            queued.IsResume.Should().BeTrue();
            queued.InitialContextJson.Should().Contain(answer, "the resumed run reads the master's answer");
        }

        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await harness.DisposeAsync();
        }
    }
}
