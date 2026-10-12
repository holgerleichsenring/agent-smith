using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using static AgentSmith.Tests.Persistence.SpecRename.OldShapeJson;
using RunEventRow = AgentSmith.Infrastructure.Persistence.Entities.RunEvent;

namespace AgentSmith.Tests.Persistence.SpecRename;

/// <summary>2026-10-12-967ab: one row of every rewritten surface, stored the way the old release stored it.</summary>
internal static class OldShapeStore
{
    public const string RunId = "run-old";

    public static readonly RunEventRow LlmCall = Row(8, nameof(EventType.LlmCallFinished),
        JsonSerializer.Serialize(new LlmCallFinishedEvent(RunId, "m", "coder", 1, 1, 0m, 1, T, Phase: "Implementation")),
        phase: "Implementation");

    public static async Task SeedAsync(AgentSmithDbContext db)
    {
        db.Runs.Add(new Run
        {
            Id = RunId, Project = "p", Pipeline = "code", TicketId = "1", Status = "running", StartedAt = T,
            PlannedStepsJson = PlannedSteps(), QueuedRequestJson = Request(), AcceptanceJson = Acceptance(),
        });
        db.RunEvents.AddRange(Events());
        db.RunCheckpoints.Add(new RunCheckpoint
        {
            RunId = RunId, Project = "p", TicketId = "1", Pipeline = "code", DialogueJobId = "j", QuestionId = "q",
            QuestionJson = "{}", RemainingCommandsJson = OldShapeJson.Commands(), ContextJson = Context(), ExecutionCount = 7,
            AskedAt = T, AnswerDeadlineAt = T,
        });
        db.RunArtifacts.AddRange(
            Artifact("phase_record:s1", "spec: s1"), Artifact("phase_review:s1", "{}"),
            Artifact("phase_record:s2", "older"), Artifact("spec_record:s2", "newer"));
        db.QueuedTickets.Add(new QueuedTicket
        {
            Project = "p", TicketId = "1", Pipeline = "code", Platform = "jira", Reason = "r", EnqueuedAt = T,
            InitialContextJson = InitialContext(),
        });
        db.ApprovedSeries.Add(new ApprovedSeries
        {
            Tracker = "t", TicketKey = "k", SeriesId = "k", TicketId = "1", Repositories = "api",
            ContentJson = ApprovedSet(), ApprovedAt = T, ApprovedInConversation = "c", ApprovedBy = "b",
        });
        db.SpecDialogSessions.Add(Session());
        db.RunSteps.Add(new RunStep
        {
            RunId = RunId, EventSeq = 4, StepIndex = 3, StepName = "Verify", Status = "ok",
            CommandName = "VerifyPhaseCommand:api", PhaseId = "s1",
        });
        db.RunDecisions.Add(new RunDecision { RunId = RunId, EventSeq = 5, Name = "a", PhaseId = "s1" });
        await db.SaveChangesAsync();
    }

    private static IEnumerable<RunEventRow> Events() =>
    [
        Row(1, "PhaseStateChanged", Event(new SpecStateChangedEvent(RunId, "s1", 1, "Read both", SpecRunState.Done, null, T))),
        Row(2, "PhaseRecorded", Event(new SpecRecordedEvent(RunId, "s1", "spec: s1", T))),
        Row(3, "PhaseReviewed", Event(new SpecReviewedEvent(RunId, "s1", "{}", T))),
        Row(4, nameof(EventType.StepStarted),
            Event(new StepStartedEvent(RunId, 3, "Select", 5, T, "Select", CommandNames.SelectSpec, "s1"))),
        Row(5, nameof(EventType.DecisionLogged), Event(new DecisionLoggedEvent(RunId, "tooling", "a", null, "b", T, "s1"))),
        Row(6, nameof(EventType.RunCheckpointed), JsonSerializer.Serialize(new RunCheckpointedEvent(
            RunId, "p", "1", null, "code", "j", "q", "{}", OldShapeJson.Commands(), Context(), 7, T, T, T))),
        Row(7, nameof(EventType.RunStoryRecorded), JsonSerializer.Serialize(new RunStoryRecordedEvent(RunId, null, Acceptance(), T))),
        LlmCall,
    ];

    private static SpecDialogSession Session() => new()
    {
        SessionId = "d1", Platform = "web", ChannelId = "c", ThreadId = "d1", UserId = "u", Project = "p",
        LastActivityAt = T, TranscriptJson = Transcript(), LatestFilingJson = Filing,
        LatestProposalJson = Outcome(new SpecOutcome(Draft) { Findings = [new ProposalFinding("s1", "p", "w")] }),
        ConfirmedOutcomeJson = Outcome(new EpicOutcome(Draft, [Draft with { SpecId = "s2" }])),
    };

    private static RunEventRow Row(long seq, string type, string payload, string? phase = null) =>
        new() { RunId = RunId, Seq = seq, Type = type, Timestamp = T, PayloadJson = payload, Phase = phase };

    private static RunArtifact Artifact(string kind, string content) => new() { RunId = RunId, Kind = kind, Content = content };
}
