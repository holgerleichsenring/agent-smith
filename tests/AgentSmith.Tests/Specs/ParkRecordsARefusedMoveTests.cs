using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Dialogue;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Resume;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets.OpenQuestions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-22-7c41b: a park whose ticket the tracker refused to move records that fact for the
/// claim gate. The status move is what makes a park persistent — without it the ticket keeps a
/// trigger status with a question on it, the run's exit releases the single-run lease, and the
/// next poll starts a second run against a question nobody has answered yet.
/// <para>
/// The rule is an outcome SET: a tracker that refused the value and a tracker whose workflow
/// offers no transition to it will both answer the same way next time. A tracker that cannot
/// express any clarification status records nothing, or every park on that tracker would put a
/// standing hold on its ticket.
/// </para>
/// </summary>
public sealed class ParkRecordsARefusedMoveTests
{
    private const string Project = "p1";
    private const string Tracker = "tracker-a";
    private const string TicketNumber = "19378";
    private const string ParkStatus = "Needs Clarification";
    private const string HandbackStatus = "needs-info";

    private readonly InMemoryUnmovedTicketStore _store = new();

    [Fact]
    public async Task Park_AStatusMoveTheTrackerRefuses_RecordsTheFactNamingTheParkingStatus()
    {
        var result = await ParkAsync(TicketFinalizeResult.Rejected(ParkStatus, "TF401347"));

        result.IsSuccess.Should().BeTrue();
        var standing = await StandingAsync();
        standing.Should().NotBeNull(
            "the ticket still carries a trigger status, so the next poll would run it again");
        standing!.ConfiguredStatus.Should().Be(ParkStatus,
            "the refusal must name the configuration field that is actually wrong");
        standing.Outcome.Should().Be(TicketFinalizeOutcome.TrackerRejectedTheStatus);
        standing.Tracker.Should().Be(Tracker);
    }

    // Jira's ordinary answer for a status its workflow cannot reach. Read as a category —
    // "only an outright rejection" — this fix would have covered one tracker and left the
    // other reporting a park that did not happen.
    [Fact]
    public async Task Park_AStatusWithNoTransitionToIt_RecordsTheFact()
    {
        await ParkAsync(TicketFinalizeResult.NoTransition(ParkStatus));

        var standing = await StandingAsync();
        standing.Should().NotBeNull("a workflow with no transition to the status answers the same next time");
        standing!.Outcome.Should().Be(TicketFinalizeOutcome.NoTransitionToTheStatus);
    }

    // The poster returns early when there are no questions. "Moved" is the zero value of the
    // outcome enum, so a defaulted answer here would read as a successful move and would CLEAR
    // a standing hold that nothing had earned.
    [Fact]
    public async Task Park_APostWithNoQuestions_RecordsNothingAndClearsNothing()
    {
        await SeedStandingFactAsync();
        var provider = Provider(TicketFinalizeResult.Rejected(ParkStatus, "TF401347"));

        var answer = await Poster(provider, out _).PostAsync(
            Pipeline(), TrackerConfig(), Ticket(), [], ParkStatus, CancellationToken.None);

        answer.StatusMoved.Should().BeFalse("nothing was asked of the tracker, so nothing moved");
        await Report().RecordParkAsync(
            Project, Tracker, new TicketId(TicketNumber), answer, CancellationToken.None);
        (await StandingAsync()).Should().NotBeNull("a post that moved nothing must clear nothing");
        provider.Verify(p => p.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // The comment-only path: no park status is configured, so the poster posts the comment and
    // asks nothing of the tracker's status. Same trap, same answer.
    [Fact]
    public async Task Park_AProjectWithNoParkStatus_RecordsNothingAndClearsNothing()
    {
        await SeedStandingFactAsync();
        var provider = Provider(TicketFinalizeResult.Rejected(ParkStatus, "TF401347"));

        var answer = await Poster(provider, out _).PostAsync(
            Pipeline(), TrackerConfig(), Ticket(), Questions(), parkStatus: null,
            CancellationToken.None);

        answer.StatusMoved.Should().BeFalse("no status was requested, so none was moved");
        await Report().RecordParkAsync(
            Project, Tracker, new TicketId(TicketNumber), answer, CancellationToken.None);
        (await StandingAsync()).Should().NotBeNull("a comment-only post must clear nothing");
        provider.Verify(p => p.UpdateStatusAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        provider.Verify(p => p.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // The checkpoint is what makes the question answerable in the dashboard. A refused park is
    // exactly the case where the fallback — a manual status move on the board — does not exist,
    // so nothing this phase does may sit between the post and the checkpoint.
    [Fact]
    public async Task Park_AStatusMoveTheTrackerRefuses_StillWritesTheCheckpoint()
    {
        var writer = new RecordingWriter();

        await ParkAsync(TicketFinalizeResult.Rejected(ParkStatus, "TF401347"), writer);

        writer.Questions.Should().ContainSingle(
            "without a checkpoint the dashboard has no question to render and nowhere to reply")
            .Which.Text.Should().Contain("May I raise the shared package?");
    }

    // Failing the run would move the ticket to the configured failed status — refused on the
    // same deployment — and lock the ticket behind a refusal naming the wrong field.
    [Fact]
    public async Task Park_AStatusMoveTheTrackerRefuses_StillReturnsSuccess()
    {
        var result = await ParkAsync(TicketFinalizeResult.Rejected(ParkStatus, "TF401347"));

        result.IsSuccess.Should().BeTrue("a parked question is an incomplete run, not a failure");
        result.Message.Should().Contain("awaiting_user_input",
            "the executor short-circuits the rest of the run on this marker");
    }

    [Fact]
    public async Task Park_AStatusMoveThatLands_ClearsAnyStandingFact()
    {
        await SeedStandingFactAsync();

        await ParkAsync(TicketFinalizeResult.Moved());

        (await StandingAsync()).Should().BeNull(
            "a corrected configuration stops refusing the ticket the first time a park succeeds");
    }

    // Two trackers express an issue's status as its open-or-closed state, so no clarification
    // status is expressible by construction — recording that would hold every parked ticket.
    [Fact]
    public async Task Park_AStatusATrackerCannotExpress_RecordsNothing()
    {
        await ParkAsync(TicketFinalizeResult.NotExpressible(ParkStatus));

        (await StandingAsync()).Should().BeNull(
            "a status no tracker of that kind can express says nothing about this ticket");
    }

    [Fact]
    public async Task Park_ARefusedMove_DoesNotLogThatTheTicketWasParked()
    {
        var posterLog = new CapturingLogger<PlanOpenQuestionsPoster>();
        var handlerLog = new CapturingLogger<MasterOpenQuestionsHandler>();

        var result = await ParkAsync(
            TicketFinalizeResult.Rejected(ParkStatus, "TF401347"),
            new RecordingWriter(), posterLog, handlerLog);

        posterLog.Lines.Concat(handlerLog.Lines).Append(result.Message ?? string.Empty)
            .Should().NotContain(line => line.Contains($"parked -> {ParkStatus}"),
                "a park the tracker refused was recorded three times as a park that happened");
    }

    [Fact]
    public async Task Handback_AStatusMoveTheTrackerRefuses_RecordsTheFact()
    {
        var provider = Provider(TicketFinalizeResult.Rejected(HandbackStatus, "TF401347"));
        var pipeline = Pipeline();
        pipeline.Set<SpecHandback>(ContextKeys.SpecHandback,
            new SpecHandback(SpecHandbackCase.RequirementsContradictRepository, "no such client here"));

        var result = await HandbackHandler(provider).ExecuteAsync(
            new SpecHandbackContext(Ticket(), HandbackTracker(), [], pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var standing = await StandingAsync();
        standing.Should().NotBeNull("the hand-back park carries the same defect on the same deployment");
        standing!.ConfiguredStatus.Should().Be(HandbackStatus);
    }

    [Fact]
    public async Task ClaimGate_ATicketWhoseParkWasRefused_IsNotClaimedAgain()
    {
        await ParkAsync(TicketFinalizeResult.Rejected(ParkStatus, "TF401347"));

        var claim = await ClaimService().ClaimAsync(
            new ClaimRequest("AzureDevOps", Project, new TicketId(TicketNumber), "code"),
            Config(), CancellationToken.None);

        claim.Outcome.Should().Be(ClaimOutcome.Rejected,
            "the next poll must refuse the ticket instead of starting a second run");
        claim.Rejection.Should().Be(ClaimRejectionReason.TicketLastLeftUnmoved);
        claim.Error.Should().Contain(ParkStatus, "the operator is told the field that is wrong");
    }

    private async Task<CommandResult> ParkAsync(
        TicketFinalizeResult finalize,
        RecordingWriter? writer = null,
        ILogger<PlanOpenQuestionsPoster>? posterLog = null,
        ILogger<MasterOpenQuestionsHandler>? handlerLog = null)
    {
        var provider = Provider(finalize);
        var poster = Poster(provider, out _, posterLog);
        var handler = new MasterOpenQuestionsHandler(
            poster,
            new ClarificationParkStatusResolver(),
            new MasterQuestionCheckpoint(
                writer ?? new RecordingWriter(),
                new DialogueJobIdentity(new Mock<IProgressReporter>().Object),
                NullLogger<MasterQuestionCheckpoint>.Instance),
            new MasterAnswerIntake(Mock.Of<IDialogueTrail>(), NullLogger<MasterAnswerIntake>.Instance),
            Report(),
            handlerLog ?? NullLogger<MasterOpenQuestionsHandler>.Instance);
        var pipeline = Pipeline();
        pipeline.Set<IReadOnlyList<PlanOpenQuestion>>(ContextKeys.MasterOpenQuestions, Questions());
        return await handler.ExecuteAsync(
            new MasterOpenQuestionsContext(
                Ticket(), TrackerConfig(), pipeline,
                new PipelineCommand(CommandNames.MasterOpenQuestions)),
            CancellationToken.None);
    }

    private SpecHandbackHandler HandbackHandler(Mock<ITicketProvider> provider) => new(
        new SpecHandbackPark(
            Factory(provider), Report(), NullLogger<SpecHandbackPark>.Instance),
        new SpecParkStatusResolver(new ClarificationParkStatusResolver()),
        new InMemorySpecSetPointerStore(),
        new SpecHandbackRepeat(NullLogger<SpecHandbackRepeat>.Instance),
        NullLogger<SpecHandbackHandler>.Instance);

    private TicketClaimService ClaimService()
    {
        var claimLock = new Mock<IRedisClaimLock>();
        claimLock.Setup(l => l.TryAcquireAsync(
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");
        claimLock.Setup(l => l.ReleaseAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var transitioner = new Mock<ITicketStatusTransitioner>();
        transitioner.Setup(t => t.TransitionAsync(
                It.IsAny<TicketId>(), It.IsAny<TicketLifecycleStatus>(),
                It.IsAny<TicketLifecycleStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransitionResult.Succeeded());
        var transitionerFactory = new Mock<ITicketStatusTransitionerFactory>();
        transitionerFactory.Setup(f => f.Create(It.IsAny<TrackerConnection>()))
            .Returns(transitioner.Object);
        return new TicketClaimService(
            claimLock.Object, _store, transitionerFactory.Object, Mock.Of<IRedisJobQueue>(),
            new NoOpActiveRunLease(), NullLogger<TicketClaimService>.Instance);
    }

    private static AgentSmithConfig Config() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            [Project] = new()
            {
                Name = Project,
                Repos = [new RepoConnection { Name = "repo-a" }],
                Tracker = TrackerConfig(),
                AzuredevopsTrigger = new WebhookTriggerConfig
                {
                    DefaultPipeline = "code",
                    TriggerStatuses = ["Active"],
                    NeedsClarificationStatus = ParkStatus,
                },
            },
        },
    };

    private UnmovedTicketReport Report() =>
        new(_store, NullLogger<UnmovedTicketReport>.Instance);

    private Task SeedStandingFactAsync() => _store.RecordAsync(
        new UnmovedTicketFact(
            Project, TicketNumber, Tracker, ParkStatus,
            TicketFinalizeOutcome.TrackerRejectedTheStatus),
        CancellationToken.None);

    private Task<UnmovedTicketFact?> StandingAsync() =>
        _store.FindStandingAsync(Project, TicketNumber, Tracker, CancellationToken.None);

    private static PlanOpenQuestionsPoster Poster(
        Mock<ITicketProvider> provider, out ServiceProvider templates,
        ILogger<PlanOpenQuestionsPoster>? logger = null)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<ITicketCommentTemplate,
            AzureDevOpsOpenQuestionsCommentTemplate>("azuredevops");
        templates = services.BuildServiceProvider();
        return new PlanOpenQuestionsPoster(
            templates, Factory(provider), new RunAnswerLink(AgentSmithConfig.Empty()),
            logger ?? NullLogger<PlanOpenQuestionsPoster>.Instance);
    }

    private static Mock<ITicketProvider> Provider(TicketFinalizeResult finalize)
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.FinalizeAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(finalize);
        return provider;
    }

    private static ITicketProviderFactory Factory(Mock<ITicketProvider> provider)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);
        return factory.Object;
    }

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "2026-09-22T21-55-07-7c41");
        pipeline.Set(ContextKeys.ProjectName, Project);
        return pipeline;
    }

    private static IReadOnlyList<PlanOpenQuestion> Questions() =>
        [new PlanOpenQuestion("q1", "May I raise the shared package?", ["yes", "no"])];

    private static TrackerConnection TrackerConfig() => new()
    {
        Name = Tracker,
        Type = TrackerType.AzureDevOps,
        NeedsClarificationStatus = ParkStatus,
    };

    private static TrackerConnection HandbackTracker() => new()
    {
        Name = Tracker,
        Type = TrackerType.AzureDevOps,
        NeedsClarificationStatus = HandbackStatus,
    };

    private static Ticket Ticket() =>
        new(new TicketId(TicketNumber), "migrate", "do it", null, "Active", "AzureDevOps");

    private sealed class RecordingWriter : IDialogueCheckpointWriter
    {
        public List<DialogQuestion> Questions { get; } = [];

        public Task<bool> TryCheckpointAsync(
            PipelineContext pipeline, DialogQuestion question, string dialogueJobId,
            CancellationToken ct)
        {
            Questions.Add(question);
            return Task.FromResult(true);
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, exception));
    }
}
