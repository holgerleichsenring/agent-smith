namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0327: a run parked on a DialogQuestion past the hot-wait threshold. Carries
/// the full checkpoint — pending question, remaining step cursor, serialized
/// pipeline context — over the event stream because that is the ONLY DB channel
/// a spawned orchestrator has (p0330 lesson); the server-side projector persists
/// it as the RunCheckpoint row. The worker task ends right after publishing;
/// RunResumer re-enters at the cursor when the answer (or the days-scale
/// timeout's DefaultAnswer) arrives.
/// </summary>
public sealed record RunCheckpointedEvent(
    string RunId,
    string Project,
    string TicketId,
    string? Platform,
    string Pipeline,
    string DialogueJobId,
    string QuestionId,
    string QuestionJson,
    string RemainingCommandsJson,
    string ContextJson,
    int ExecutionCount,
    DateTimeOffset AskedAt,
    DateTimeOffset AnswerDeadlineAt,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.RunCheckpointed, Timestamp);
