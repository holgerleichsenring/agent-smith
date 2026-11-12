using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Turns;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Events;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-11-10-d854c: a design turn is its run — its steps are run events read run-wide, the conversation's row and its
/// kept turn name the run, and each turn's own proposal is kept by run id beside the latest one.
/// </summary>
public sealed class TurnRunRecordTests : IDisposable
{
    private const string Dialog = "d-run-record";
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AgentSmithDbContext _context;
    private readonly IServiceScopeFactory _scopes;

    public TurnRunRecordTests()
    {
        _connection.Open();
        _context = new AgentSmithDbContext(Options());
        MigratedStoreTemplate.CopyInto(_context);
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        _scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    [Fact]
    public async Task DialogActivityObserver_Step_IsRecordedOnTheTurnsRunWithBoundedDetail()
    {
        var published = new List<RunEvent>();
        var publisher = new Mock<IEventPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback((RunEvent e, CancellationToken _) => published.Add(e)).Returns(Task.CompletedTask);
        var runs = new AsyncLocalRunContextAccessor();
        var recorder = new DialogActivityRecorder(publisher.Object, runs, NullLogger<DialogActivityRecorder>.Instance);
        var channel = new DashboardActivityChannel(Mock.Of<ITurnActivityObserverAccessor>(), NullLogger<DashboardActivityChannel>.Instance);
        var observer = new DialogActivityObserver(channel, Dialog, new RunningDialogTurn(TimeProvider.System), recorder);

        using (runs.BeginScope("run-7"))
            await observer.ReportAsync(new TurnActivity(TurnActivityKind.Tool, "read_file", new string('x', 900)), default);

        var step = published.Should().ContainSingle().Which.Should().BeOfType<DialogActivityEvent>().Subject;
        step.RunId.Should().Be("run-7");
        step.Detail.Should().HaveLength(DialogActivityEvent.MaxDetail);
        (step.Kind, step.Name, step.Seq).Should().Be(("tool", "read_file", 1));
    }

    // d854c review: a chat platform's turn keeps its steps on its run too; nothing is sent to a dialog group.
    [Fact]
    public async Task DashboardActivityChannel_SlackTurn_RecordsEachStepOnItsRun()
    {
        var published = new List<RunEvent>();
        var publisher = new Mock<IEventPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback((RunEvent e, CancellationToken _) => published.Add(e)).Returns(Task.CompletedTask);
        var runs = new AsyncLocalRunContextAccessor();
        var observers = new AsyncLocalTurnActivityObserverAccessor();
        var hub = new RecordingDialogHub();
        var channel = new DashboardActivityChannel(observers, NullLogger<DashboardActivityChannel>.Instance, hub,
            new DialogActivityRecorder(publisher.Object, runs, NullLogger<DialogActivityRecorder>.Instance));
        var slack = new ConversationState
        {
            JobId = "s-slack", Platform = "slack", ChannelId = "C1", ThreadId = "1700.01", UserId = "U1", Project = "p", TicketId = "",
            StartedAt = DateTimeOffset.UnixEpoch,
        };

        using (runs.BeginScope("run-chat"))
        using (channel.Observe(slack, new RunningDialogTurn(TimeProvider.System)))
            await observers.Current!.ReportAsync(new TurnActivity(TurnActivityKind.Tool, "read_file", null), default);

        published.Should().ContainSingle().Which.Should().BeOfType<DialogActivityEvent>().Which.RunId.Should().Be("run-chat");
        hub.Pushes.Should().BeEmpty();
    }

    [Fact]
    public async Task DialogActivityReader_StepsAcrossPipelineSteps_PagesAllInOrderWithTheirCount()
    {
        var at = DateTimeOffset.UnixEpoch;
        RunEvent[] trail =
        [
            Step(1, at) with { OriginStepIndex = 2 }, new RunStartedEvent("run-1", "spec-dialog", "p", null, at),
            Step(2, at) with { OriginStepIndex = 2 }, Step(3, at) with { OriginStepIndex = 5 }, Step(4, at) with { OriginStepIndex = 6 },
        ];
        await using (var ctx = new AgentSmithDbContext(Options()))
        {
            for (var seq = 0; seq < trail.Length; seq++) ctx.RunEvents.Add(RunTrailRowMapper.Map("run-1", 10 + seq, trail[seq]));
            await ctx.SaveChangesAsync();
        }
        var reader = new DialogActivityReader(_scopes, new EventEnvelopeSerializer());

        var first = await reader.ReadAsync("run-1", null, 3, default);
        var rest = await reader.ReadAsync("run-1", first.NextSeq, 3, default);

        first.Count.Should().Be(4);
        first.HasMore.Should().BeTrue();
        rest.HasMore.Should().BeFalse();
        first.Steps.Concat(rest.Steps).Select(s => s.Seq).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task DialogActivityReader_TheTurnsSpan_IsItsFirstAndLastStepWithItsSubAgentCalls() // 2026-11-10-d854b
    {
        var start = DateTimeOffset.UnixEpoch;
        RunEvent[] trail =
        [
            Step(1, start), Step(2, start.AddMinutes(3)) with { Name = "spawn_agents" }, Step(3, start.AddMinutes(20)),
        ];
        await using (var ctx = new AgentSmithDbContext(Options()))
        {
            for (var seq = 0; seq < trail.Length; seq++) ctx.RunEvents.Add(RunTrailRowMapper.Map("run-1", seq, trail[seq]));
            await ctx.SaveChangesAsync();
        }

        var page = await new DialogActivityReader(_scopes, new EventEnvelopeSerializer()).ReadAsync("run-1", null, 1, default);

        (page.FirstAt, page.LastAt, page.SubAgentCalls).Should().Be((start, start.AddMinutes(20), 1));
    }

    [Fact]
    public async Task GroundSpecDialog_ReportsTheRunIdThroughTheTurnRunRecorder()
    {
        var accessor = new AsyncLocalTurnRunRecorderAccessor();
        var recorder = new Mock<ITurnRunRecorder>();
        var slot = new SpecDialogReplySlot();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-3");
        pipeline.Set(ContextKeys.SpecDialogReplySlot, slot);
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, []);
        var handler = new GroundSpecDialogHandler(null!, NullLogger<GroundSpecDialogHandler>.Instance, runs: accessor);

        using (accessor.Observe(recorder.Object))
            await handler.ExecuteAsync(new GroundSpecDialogContext(pipeline), CancellationToken.None);

        slot.RunId.Should().Be("run-3");
        recorder.Verify(r => r.RecordAsync("run-3", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TurnRunRecorder_WritesTheRunningRunIdOnTheSession()
    {
        var harness = new SpecDialogDecisionHarness(_context);
        var state = await harness.OpenAsync("dashboard", Dialog);

        await new SessionTurnRunRecorder(_scopes, state, NullLogger<SessionTurnRunRecorder>.Instance)
            .RecordAsync("run-9", CancellationToken.None);

        (await new SpecDialogDecisionHarness(new AgentSmithDbContext(Options())).Sessions
            .GetOpenByThreadAsync("dashboard", Dialog, default))!.RunningRunId.Should().Be("run-9");
    }

    [Fact]
    public async Task ProposalStore_ObserverAndFlow_BothWriteTheTurnsRow()
    {
        var harness = new SpecDialogDecisionHarness(_context);
        var state = await harness.OpenAsync("dashboard", Dialog);
        var runs = new AsyncLocalRunContextAccessor();
        var observer = new DialogProposalObserver(Scopes(harness, runs), state, NullLogger<DialogProposalObserver>.Instance);

        using (runs.BeginScope("run-1")) await observer.PublishAsync(Bug("draft") with { UnderReview = true }, default);
        var afterDraft = await harness.TurnProposals.ReadAsync(state.JobId, default);
        await harness.Flow().HandleAsync(state, Bug("final"), default, runId: "run-1");

        afterDraft["run-1"].Proposal.UnderReview.Should().BeTrue("the observer wrote the turn's draft while it was reviewed");
        var kept = await harness.TurnProposals.ReadAsync(state.JobId, default);
        kept.Should().ContainSingle();
        ((BugOutcome)kept["run-1"].Proposal).Ticket.Title.Should().Be("final", "the flow replaced the draft with the turn's proposal");
    }

    [Fact]
    public async Task ProposalStore_TwoProposingTurns_BothReadableByRunId()
    {
        var harness = new SpecDialogDecisionHarness(_context);
        var state = await harness.OpenAsync("dashboard", Dialog);
        foreach (var (run, title) in new[] { ("run-1", "first"), ("run-2", "second") })
        {
            await harness.Sessions.AppendTurnAsync("dashboard", Dialog, TranscriptRole.Assistant, title, null, null, default, runId: run);
            await harness.Flow().HandleAsync(state, Bug(title), default, runId: run);
        }
        var current = (await harness.Sessions.GetOpenByThreadAsync("dashboard", Dialog, default))!;
        var view = new SpecDialogSessionView(current.JobId, new SpecDialogProjectView("sample", [], []),
            SpecDialogShownTranscript.Turns(current.Transcript), current.LastActivityAt, []);

        var read = await new SpecDialogTurnProposals(harness.TurnProposals,
            new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()),
            new AgentSmith.Infrastructure.Persistence.Repositories.RunLivenessRepository(_context)).WithAsync(view, current, false, default);

        read.Transcript.Where(t => t.RunId is not null).Select(t => t.Proposal?.Bug?.Title).Should()
            .Equal(["first", "second"], "the second proposal superseded the first as the card, not as the first turn's own");
    }

    // 2026-11-12-01b4: a tool-written proposal leaves no draft block in the reply, so the text anchor finds no turn and the
    // card trailed the transcript as a second copy; the awaiting proposal sits on the reply whose run kept it.
    [Fact]
    public async Task TurnProposals_ToolWrittenAwaitingProposal_SitsOnItsOwnReply()
    {
        var harness = new SpecDialogDecisionHarness(_context);
        var state = await harness.OpenAsync("dashboard", Dialog);
        await harness.Sessions.AppendTurnAsync("dashboard", Dialog, TranscriptRole.User, "cut it", null, null, default);
        await harness.Sessions.AppendTurnAsync("dashboard", Dialog, TranscriptRole.Assistant, "first", null, null, default, runId: "run-1");
        await harness.Flow().HandleAsync(state, Bug("first"), default, runId: "run-1");
        await harness.Sessions.AppendTurnAsync("dashboard", Dialog, TranscriptRole.Assistant, "second", null, null, default, runId: "run-2");
        await harness.Flow().HandleAsync(state, Bug("second"), default, runId: "run-2");
        await harness.Sessions.AppendTurnAsync("dashboard", Dialog, TranscriptRole.Assistant, "a plain answer", null, null, default, runId: "run-3");
        var current = (await harness.Sessions.GetOpenByThreadAsync("dashboard", Dialog, default))!;
        var awaiting = (await harness.Outcomes.ReadAsync("dashboard", Dialog, default)).Proposal!;
        var composer = new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer());
        var view = new SpecDialogSessionView(current.JobId, new SpecDialogProjectView("sample", [], []),
            SpecDialogShownTranscript.Turns(current.Transcript), current.LastActivityAt, [],
            Proposal: composer.Compose(Dialog, awaiting, current.LastActivityAt),
            ProposalTurn: SpecDialogShownTranscript.CardTurn(current.Transcript));

        var read = await new SpecDialogTurnProposals(harness.TurnProposals, composer,
            new AgentSmith.Infrastructure.Persistence.Repositories.RunLivenessRepository(_context)).WithAsync(view, current, false, default, awaiting);

        view.ProposalTurn.Should().BeNull("no reply's text holds a draft block");
        read.ProposalTurn.Should().Be(2, "the reply of run-2 kept the awaiting proposal; run-3 only answered");
    }

    private static DialogActivityEvent Step(int seq, DateTimeOffset at) => new("run-1", at, "tool", "read_file", "a.cs", seq, at);

    private static BugOutcome Bug(string title) => new(new BugTicketDraft(title, "It vanished.", null));

    private static IServiceScopeFactory Scopes(SpecDialogDecisionHarness harness, IRunContextAccessor runs)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => harness.Proposals).AddScoped(_ => harness.Channel).AddScoped(_ => harness.TurnProposals);
        services.AddSingleton(runs);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
