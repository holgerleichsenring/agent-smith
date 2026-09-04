using AgentSmith.Contracts.Events;

namespace AgentSmith.Infrastructure.Services.Events;

/// <summary>
/// p0405: maps an event's wire discriminator to the concrete record that carries
/// it. Split out of <see cref="EventEnvelopeSerializer"/> — the serializer owns
/// the envelope shape, this owns which type each code means, and only the latter
/// grows by one line every time an event is added. Pure functions over enum
/// values: no state, no collaborators, so no DI.
/// </summary>
public static class EventTypeResolver
{
    public static Type? Resolve(EventType type) => type switch
    {
        EventType.RunStarted => typeof(RunStartedEvent),
        EventType.RunFinished => typeof(RunFinishedEvent),
        EventType.SandboxCreated => typeof(SandboxCreatedEvent),
        EventType.SandboxDisposed => typeof(SandboxDisposedEvent),
        EventType.StepStarted => typeof(StepStartedEvent),
        EventType.StepFinished => typeof(StepFinishedEvent),
        EventType.DecisionLogged => typeof(DecisionLoggedEvent),
        EventType.GateChecked => typeof(GateCheckedEvent),
        EventType.TriageRoute => typeof(TriageRouteEvent),
        EventType.LlmCallStarted => typeof(LlmCallStartedEvent),
        EventType.LlmCallFinished => typeof(LlmCallFinishedEvent),
        EventType.SandboxCommand => typeof(SandboxCommandEvent),
        EventType.SandboxOutput => typeof(SandboxOutputEvent),
        EventType.SandboxResult => typeof(SandboxResultEvent),
        EventType.ToolCall => typeof(ToolCallEvent),
        EventType.ToolResult => typeof(ToolResultEvent),
        EventType.L1StepDetail => typeof(L1StepDetailEvent),
        EventType.TicketFetched => typeof(TicketFetchedEvent),
        EventType.CatalogLoaded => typeof(CatalogLoadedEvent),
        EventType.PullRequestOutcome => typeof(PullRequestOutcomeEvent), // 2026-09-03-b028
        EventType.CatalogIssue => typeof(CatalogIssueEvent),
        EventType.TicketInstructionIgnored => typeof(TicketInstructionIgnoredEvent), // 2026-09-03-b028
        EventType.SubAgentSpawned => typeof(SubAgentSpawnedEvent),
        EventType.SubAgentObservation => typeof(SubAgentObservationEvent),
        EventType.SubAgentFinding => typeof(SubAgentFindingEvent),
        EventType.SubAgentFileWritten => typeof(SubAgentFileWrittenEvent),
        EventType.SubAgentToolCall => typeof(SubAgentToolCallEvent),
        EventType.SubAgentCompleted => typeof(SubAgentCompletedEvent),
        EventType.RunCancelRequested => typeof(RunCancelRequestedEvent),
        EventType.SandboxVanished => typeof(SandboxVanishedEvent),
        EventType.RunCheckpointed => typeof(RunCheckpointedEvent), // p0327
        EventType.ExpectationRatified => typeof(ExpectationRatifiedEvent), // p0328
        EventType.RunStoryRecorded => typeof(RunStoryRecordedEvent), // p0344b
        EventType.RunBudgetResolved => typeof(RunBudgetResolvedEvent), // p0357
        EventType.LedgerTransitionsRecorded => typeof(LedgerTransitionsRecordedEvent), // p0374a
        EventType.PipelineStepsPlanned => typeof(PipelineStepsPlannedEvent), // p0405
        EventType.RunWorkShapeResolved => typeof(RunWorkShapeResolvedEvent), // p0413
        EventType.PhaseStateChanged => typeof(PhaseStateChangedEvent), // p0466
        EventType.PhaseRecorded => typeof(PhaseRecordedEvent), // p0466
        _ => null
    };

    // p0173a: slice a defined only the enum codes; p0173b adds the poller +
    // webhook records. Slice c will add the chat / config / catalog rows.
    public static Type? ResolveSystem(SystemEventType type) => type switch
    {
        SystemEventType.PollCycleStarted => typeof(PollCycleStartedEvent),
        SystemEventType.PollCycleFinished => typeof(PollCycleFinishedEvent),
        SystemEventType.TicketScanned => typeof(TicketScannedEvent),
        SystemEventType.TicketSkipped => typeof(TicketSkippedEvent),
        SystemEventType.TicketTriggered => typeof(TicketTriggeredEvent),
        SystemEventType.WebhookReceived => typeof(WebhookReceivedEvent),
        SystemEventType.ChatMessageReceived => typeof(ChatMessageReceivedEvent),
        SystemEventType.ConfigFileRead => typeof(ConfigFileReadEvent),
        SystemEventType.SkillCatalogLoaded => typeof(SkillCatalogLoadedEvent),
        SystemEventType.ConceptVocabularyLoaded => typeof(ConceptVocabularyLoadedEvent),
        SystemEventType.ConfigChanged => typeof(ConfigChangedEvent), // p0353
        SystemEventType.ConfigReloaded => typeof(ConfigReloadedEvent), // p0353
        _ => null
    };
}
