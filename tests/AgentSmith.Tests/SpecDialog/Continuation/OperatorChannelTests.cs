using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Application.Services.Rework;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using AgentSmith.Tests.TestSupport;
using AgentSmith.Tests.Tickets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-9a59m: approving in a ticket's own conversation is an operator act. The approval is recorded,
/// an Operator nudge goes through the durable queue, the real rework worker's reads find the stored approval
/// as the act, and the attempt that starts names it as its cause — over one migrated store.
/// </summary>
public sealed class OperatorChannelTests : IDisposable
{
    private const string Series = "2026-10-13-ab12";
    private static readonly string A = Series + "a", B = Series + "b";

    private readonly ContinuationHarness _h = new();
    private readonly Mock<IReworkEntry> _entry = new();

    public OperatorChannelTests() =>
        _entry.Setup(e => e.EnterAsync(It.IsAny<ResolvedProject>(), "42", It.IsAny<ReworkAct>(), "code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReworkOutcome.Started("r-2"));

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task BoundApproval_FinishedAttempt_StartsOneAttempt_CauseOperator()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A], status: RunStatuses.Failed);
        var proposal = await BindAsync();

        await _h.AcceptAsync(proposal, mayStartRuns: true);

        (await _h.NudgesAsync()).Should().ContainSingle(n => n.Origin == (int)ReworkNudgeOrigin.Operator && n.Channel == "Operator");
        (await TranscriptAsync()).Should().Contain("The next attempt starts now.");
        (await HandleNextAsync()).Should().Be(ReworkNudgeDisposition.Finish);
        var approval = (await _h.RecordAsync())!.Approval!;
        _entry.Verify(e => e.EnterAsync(It.IsAny<ResolvedProject>(), "42",
            It.Is<ReworkAct>(a => a.Channel == ReworkChannel.Operator && a.Author == "owner" && a.At == approval.At),
            "code", It.IsAny<CancellationToken>()), Times.Once);

        var cause = await CauseOfNextAttemptAsync("r-2");
        cause.Should().BeEquivalentTo(new AttemptCause(AttemptChannels.Operator, "owner", approval.At,
            new AttemptSource(AttemptSourceKinds.Conversation, Id: ContinuationHarness.SessionId)));

        await EndAsync("r-2", readAt: DateTimeOffset.UtcNow, workedApproval: approval.At);
        await EnqueueAsync(ReworkNudgeOrigin.RunEnd);
        await HandleNextAsync();
        _entry.Verify(e => e.EnterAsync(It.IsAny<ResolvedProject>(), It.IsAny<string>(), It.IsAny<ReworkAct>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once, "the attempt that read the approval served it");
    }

    [Fact]
    public async Task BoundApproval_NeverWorked_StartsTheFirstAttemptThroughTheMove()
    {
        _h.Routing = Routing("agentsmith-alpha");
        _h.Config.Projects["alpha"] = _h.Routing.Projects["alpha"]; // the conversation's project resolves by Tag
        var proposal = Lone(Draft("p-1", "the widget stops dropping"));
        await _h.BindAsync(proposal);

        await _h.AcceptAsync(proposal, mayStartRuns: true);

        var record = (await _h.RecordAsync())!;
        record.Continuation.Should().Be(new SeriesContinuation([], null, StartsAttempt: true));
        _h.Tracker.Labels.Should().Equal(["agentsmith-alpha"], "the routing tag only — no stamp that would re-route it");
        (await TranscriptAsync()).Should().Contain("first attempt starts");
        (await _h.NudgesAsync()).Should().BeEmpty("the worker has no attempt to reopen; the move starts it");
        (await CauseOfNextAttemptAsync("r-1")).Channel.Should().Be(AttemptChannels.Operator, "the start's move never reads as pickup");
    }

    // 2026-10-14-dbceh: a held ticket's approval says what it waits for; the wake starts it once that landed.
    [Fact]
    public async Task OperatorStart_HeldNeverWorkedTicket_SaysWhatItWaitsForAndMovesNothing()
    {
        _h.Routing = Routing("agentsmith-alpha");
        _h.Config.Projects["alpha"] = _h.Routing.Projects["alpha"];
        _h.Facts.Edges.Add(Requires("41"));
        var proposal = Lone(Draft("p-1", "the widget stops dropping"));
        await _h.BindAsync(proposal);

        await _h.AcceptAsync(proposal, mayStartRuns: true);

        (await TranscriptAsync()).Should().Contain("This ticket waits for #41 to be merged first")
            .And.NotContain("first attempt starts");
        _h.Tracker.Labels.Should().BeEmpty("nothing tags or moves a held ticket");
        _h.Tracker.Moves.Should().BeEmpty();
    }

    [Fact]
    public async Task OperatorStart_ApprovalInAnIncompleteFilingsConversation_StartsOnceItsRequirementsLanded()
    {
        _h.Routing = Routing("agentsmith-alpha");
        _h.Config.Projects["alpha"] = _h.Routing.Projects["alpha"];
        _h.Facts.Edges.Add(Requires("41"));
        var gate = new AgentSmith.Application.Services.Claim.RequiresUnmetGate(TicketRequirementDoubles.Of(_h.Facts, _h.Approvals));
        var claim = new ClaimRequest("GitHub", "alpha", new TicketId("42"), "code");
        (await gate.RefusalAsync(claim, _h.Config, default))!.Error.Should().Be(
            AgentSmith.Application.Services.Claim.RequiresUnmetGate.IncompleteFiling, "edges without a record are an unfinished filing");
        var proposal = Lone(Draft("p-1", "the widget stops dropping"));
        await _h.BindAsync(proposal);

        await _h.AcceptAsync(proposal, mayStartRuns: true);
        _h.Facts.Landed.Add(new TicketLandingFact("gh", "github-41", "41", "alpha", TicketLandingKinds.Merged,
            DateTimeOffset.UnixEpoch, null, null, null));

        (await gate.RefusalAsync(claim, _h.Config, default)).Should().BeNull("the approval completed the filing and 41 landed");
        _h.Tracker.Labels.Should().BeEmpty("the approval itself started nothing; the wake does");
    }

    private static TicketRequirementEdge Requires(string required) =>
        new("gh", ContinuationHarness.Key, ContinuationHarness.TicketId, "alpha", $"github-{required}", required, true, default,
            ContinuationHarness.SessionId);

    [Fact]
    public async Task BoundApproval_LiveRun_ServedByRunEndNudge()
    {
        await LiveAsync();
        var proposal = await BindAsync();

        await _h.AcceptAsync(proposal, mayStartRuns: true);

        (await TranscriptAsync()).Should().Contain("Run r-1 works this ticket now");
        (await HandleNextAsync()).Should().Be(ReworkNudgeDisposition.Finish, "a leased run's end nudges again");
        _entry.VerifyNoOtherCalls();

        await EndLiveAsync();
        await HandleNextAsync();
        _entry.Verify(e => e.EnterAsync(It.IsAny<ResolvedProject>(), "42",
            It.Is<ReworkAct>(a => a.Channel == ReworkChannel.Operator), "code", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BoundApproval_LiveRunPublishedTheApproval_StartsNoExtraAttempt()
    {
        await LiveAsync();
        var proposal = await BindAsync();
        await _h.AcceptAsync(proposal, mayStartRuns: true);
        await HandleNextAsync();

        var approval = (await _h.RecordAsync())!.Approval!;
        await UpdateAsync<RunAttempt>(a => a.RunId == "r-1", a => a.ApprovalAt = approval.At);
        await EndLiveAsync();
        await HandleNextAsync();

        _entry.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BoundApproval_QuestionPending_RecordsNothing_NamesAnswerPath_ProposalKept()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A], status: RunStatuses.WaitingForInput);
        await _h.InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IRunCheckpointStore>().SaveAsync(new RunCheckpointRecord(
                "r-1", "alpha", "42", null, "code", "job", "q-1", "{}", "[]", "{}", 1, TicketStoreHarness.T,
                TicketStoreHarness.T.AddDays(1), null), default);
            return true;
        });
        var proposal = await BindAsync();

        await _h.AcceptAsync(proposal, mayStartRuns: true);

        (await _h.RecordAsync()).Should().BeNull("an approval while a question waits records nothing");
        (await _h.OpenProposalJsonAsync()).Should().NotBeNull("the same proposal is approved once the question is answered");
        (await TranscriptAsync()).Should().Contain("POST /api/runs/r-1/answer");
        (await _h.NudgesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task BoundApproval_WithoutRunsControl_RunEndStartsNothing()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A], status: RunStatuses.Failed);
        var proposal = await BindAsync();

        await _h.AcceptAsync(proposal, mayStartRuns: false);

        (await _h.RecordAsync())!.Continuation!.StartsAttempt.Should().BeFalse();
        (await TranscriptAsync()).Should().Contain("runs.control");
        (await _h.NudgesAsync()).Should().BeEmpty();
        await EnqueueAsync(ReworkNudgeOrigin.RunEnd);
        await HandleNextAsync();
        _entry.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OperatorNudge_TwoReplicas_OneAttempt()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A], status: RunStatuses.Failed);
        var proposal = await BindAsync();
        await _h.AcceptAsync(proposal, mayStartRuns: true);
        await EnqueueAsync(ReworkNudgeOrigin.Operator);

        var first = await ClaimAsync();
        var second = await ClaimAsync();

        (await _h.NudgesAsync()).Should().ContainSingle("a second Operator nudge merges into the pending one");
        first.Should().ContainSingle();
        second.Should().BeEmpty("the first replica's claim holds the ticket's nudge");
        await Handler().HandleAsync(_h.Config.Projects["alpha"], first[0], default);
        _entry.Verify(e => e.EnterAsync(It.IsAny<ResolvedProject>(), "42", It.IsAny<ReworkAct>(), "code",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OperatorNudge_AfterOperatorStop_IsWithheld()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A], status: RunStatuses.Failed);
        var proposal = await BindAsync();
        await _h.AcceptAsync(proposal, mayStartRuns: true);
        var approval = (await _h.RecordAsync())!.Approval!;
        await _h.InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<ReworkLedgerRepository>().RaiseAsync("alpha", "42", approval.At.AddMinutes(1), default);
            return true;
        });

        (await HandleNextAsync()).Should().Be(ReworkNudgeDisposition.Finish);

        _entry.VerifyNoOtherCalls();
        _h.Tracker.Comments.Should().ContainSingle(c => c.TicketId == "42", "a withheld act is said once on the ticket");
    }

    [Fact]
    public async Task ApprovalPreview_NamesTheStartArm_ForTheCallersPermission()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A], status: RunStatuses.Failed);
        await BindAsync();

        (await _h.PreviewAsync(mayStartRuns: true))!.View!.Start.Should().Be(new TicketContinuationStart(TicketStartArms.StartNow));
        (await _h.PreviewAsync(mayStartRuns: false))!.View!.Start.Should().Be(new TicketContinuationStart(TicketStartArms.WithoutRunsControl));
        (await _h.NudgesAsync()).Should().BeEmpty("a preview starts nothing");
    }

    private async Task<SpecOutcome> BindAsync()
    {
        var proposal = Lone(Draft(B, "B, re-cut"));
        await _h.BindAsync(proposal);
        return proposal;
    }

    private async Task LiveAsync()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A], status: "running");
        await _h.InScopeAsync(async sp =>
        {
            var leases = sp.GetRequiredService<ActiveRunRepository>();
            await leases.TryClaimAsync("alpha", new TicketId("42"), default);
            await leases.AttachRunAsync("alpha", new TicketId("42"), "r-1", null, default);
            return true;
        });
    }

    // The run ends: its row turns terminal, then the lease's release enqueues the RunEnd nudge.
    private async Task EndLiveAsync()
    {
        await UpdateAsync<Run>(r => r.Id == "r-1", r => { r.Status = RunStatuses.Failed; r.FinishedAt = DateTimeOffset.UtcNow; });
        await _h.InScopeAsync(async sp =>
            await sp.GetRequiredService<ActiveRunRepository>().ReleaseAsync("alpha", new TicketId("42"), "r-1", default));
    }

    // The attempt that served the approval ended: it read the ticket after it and published it as worked.
    private Task EndAsync(string runId, DateTimeOffset readAt, DateTimeOffset workedApproval) => _h.AddAsync(
        WithRead(TicketStoreHarness.Run(runId, status: RunStatuses.Failed), readAt), TicketStoreHarness.Step(runId),
        new RunAttempt { RunId = runId, ApprovalAt = workedApproval });

    private static Run WithRead(Run run, DateTimeOffset readAt)
    {
        run.ActsReadAt = readAt;
        return run;
    }

    private Task EnqueueAsync(ReworkNudgeOrigin origin) =>
        _h.Services.GetRequiredService<IReworkNudges>().EnqueueAsync(new ReworkNudgeRequest("alpha", "42", origin), default);

    private Task<IReadOnlyList<ClaimedReworkNudge>> ClaimAsync() =>
        _h.InScopeAsync(sp => sp.GetRequiredService<ReworkNudgeRepository>().ClaimDueAsync(10, default));

    // One worker pass: claim, handle, finish — what ReworkNudgeWorker does with a claimed row.
    private async Task<ReworkNudgeDisposition> HandleNextAsync()
    {
        var claimed = (await ClaimAsync()).Single();
        var disposition = await Handler().HandleAsync(_h.Config.Projects["alpha"], claimed, default);
        await _h.InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<ReworkNudgeRepository>().FinishAsync(claimed, default);
            return true;
        });
        return disposition;
    }

    private ReworkNudgeHandler Handler()
    {
        var sp = _h.Services;
        var scopes = sp.GetRequiredService<IServiceScopeFactory>();
        var speech = new ReworkSpeech(scopes, _h.Tracker, Mock.Of<ISourceProviderFactory>(), NullLogger<ReworkSpeech>.Instance);
        var fallback = new ReworkFallbackSpawn(Mock.Of<IConfigurationLoader>(), new ServerContext("c.yml"), _h.Tracker,
            new PolledTicketEnvelope(ApprovedRecordProbes.None()), Mock.Of<IEnvelopeProjectResolver>(),
            Mock.Of<ISpawnPipelineRunsUseCase>(), NullLogger<ReworkFallbackSpawn>.Instance);
        var pending = new ReworkPendingActs(_h.Tracker, Mock.Of<ISourceProviderFactory>(), Mock.Of<IPrReviewAuthorTrust>(), _h.Approvals);
        return ReworkHandlers.Of(sp.GetRequiredService<IActiveRunLease>(), sp.GetRequiredService<IPreviousAttemptReader>(),
            pending, _entry.Object, speech, fallback, new ReworkWithheld(scopes, speech), sp.GetRequiredService<IRunCheckpointStore>(),
            ClosedTicketFollowUps.Of(scopes, _h.Tracker, speech));
    }

    // The started run's FetchTicket: the ticket, its tracker connection and its previous attempt, as the run sets them.
    private async Task<AttemptCause> CauseOfNextAttemptAsync(string runId)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Ticket, new Ticket(new TicketId("42"), "Widget drops", "d", null, "Open", "github"));
        pipeline.Set(ContextKeys.TrackerConnection, "gh");
        pipeline.Set(ContextKeys.PipelineName, PipelinePresets.CodeName);
        if (await _h.Services.GetRequiredService<IPreviousAttemptReader>().LatestAsync("alpha", "42", runId, default) is { } previous)
            pipeline.Set(ContextKeys.PreviousAttempt, previous);
        var reader = new AttemptCauseReader(new TrackerIdentity(_h.Tracker, NullLogger<TrackerIdentity>.Instance),
            new ApprovedSpecSetResolver(_h.Approvals, NullLogger<ApprovedSpecSetResolver>.Instance), NullLogger<AttemptCauseReader>.Instance);
        return await reader.ReadAsync(_h.Tracker.Create(new TrackerConnection()),
            new FetchTicketContext(new TicketId("42"), new TrackerConnection { Name = "gh", Type = TrackerType.GitHub }, pipeline, null, null),
            default);
    }

    private async Task UpdateAsync<T>(Func<T, bool> match, Action<T> change) where T : class
    {
        await _h.InScopeAsync(async sp =>
        {
            var uow = sp.GetRequiredService<AgentSmith.Infrastructure.Persistence.Contracts.IUnitOfWork>();
            foreach (var row in uow.Set<T>().AsEnumerable().Where(match)) change(row);
            await uow.SaveChangesAsync(default);
            return true;
        });
    }

    private Task<string> TranscriptAsync() => _h.InScopeAsync(async sp =>
        (await sp.GetRequiredService<SpecDialogSessionRepository>().GetBySessionIdAsync(ContinuationHarness.SessionId, default))!
            .TranscriptJson);

    private static AgentSmithConfig Routing(string tag)
    {
        var project = new ResolvedProject
        {
            Name = "alpha", DefaultPipeline = "code",
            Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
            Repos = [new RepoConnection { Name = "api", DefaultBranch = "main" }],
            GithubTrigger = new WebhookTriggerConfig
            {
                DefaultPipeline = "code", TriggerStatuses = ["open"],
                ProjectResolution = new ProjectResolutionConfig { Strategy = ResolutionStrategy.Tag, Value = tag },
            },
        };
        return new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject> { ["alpha"] = project },
            PipelineTriggers = PipelineTriggerMap.Empty,
        };
    }

    private static SpecOutcome Lone(SpecDraft draft) => new(draft);

    private static SpecDraft Draft(string id, string goal) =>
        new(id, goal, $"spec: {id}\ngoal: \"{goal}\"\ndone:\n  - \"{goal} works.\"\n", []) { Repos = ["api"], Done = [$"{goal} works."] };
}
