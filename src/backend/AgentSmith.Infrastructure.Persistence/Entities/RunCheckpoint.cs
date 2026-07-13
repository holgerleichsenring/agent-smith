namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// p0327: the durable form of a run parked on a DialogQuestion
/// (Run.Status=waiting_for_input). Projected from RunCheckpointedEvent —
/// serialized pipeline context + step cursor + the pending question. ResumedAt
/// null = still waiting; set = a resume was enqueued (the sweeper's idempotence
/// marker). Relational following the SpecDialogSession precedent: volatile
/// Redis must never be the only holder of a multi-day wait.
/// </summary>
public sealed class RunCheckpoint : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public string Pipeline { get; set; } = string.Empty;
    public string DialogueJobId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public string QuestionJson { get; set; } = string.Empty;
    public string RemainingCommandsJson { get; set; } = string.Empty;
    public string ContextJson { get; set; } = string.Empty;
    public int ExecutionCount { get; set; }
    public DateTimeOffset AskedAt { get; set; }
    public DateTimeOffset AnswerDeadlineAt { get; set; }
    public DateTimeOffset? ResumedAt { get; set; }
}
