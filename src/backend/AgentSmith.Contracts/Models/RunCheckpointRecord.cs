namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0327: one persisted run checkpoint — the durable form of a run parked on a
/// DialogQuestion. Projected from RunCheckpointedEvent by the server-side
/// applier; consumed by the dialogue resume sweeper + RunResumer. ResumedAt is
/// the consumed marker: null = still waiting for an answer, set = a resume was
/// enqueued (idempotence guard for the sweeper and duplicate answers).
/// </summary>
public sealed record RunCheckpointRecord(
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
    DateTimeOffset? ResumedAt);
