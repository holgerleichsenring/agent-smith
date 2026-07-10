namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// The system-of-record row for one pipeline run. Id is the sortable run id
/// (p0156 ISO-8601 + hex). Holds the run-level facts the dashboard reads; the
/// child collections carry the trail. 1—N to every child; 1—1 to ActiveRun.
/// </summary>
public sealed class Run : EntityBase
{
    public string Id { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string Pipeline { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;
    public string? TicketTitle { get; set; }
    public string? Platform { get; set; }
    public string? Trigger { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RepoMode { get; set; }
    public string? AgentName { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    // p0322a: the producer's live step total (StepStartedEvent.TotalSteps),
    // persisted as the max seen — it GROWS mid-run when BootstrapDispatch
    // splices skill rounds into the command list. Null on pre-p0322a rows.
    public int? TotalSteps { get; set; }
    public double? DurationSeconds { get; set; }
    public string? Summary { get; set; }
    public decimal CostTotalUsd { get; set; }
    public long TokensIn { get; set; }
    public long TokensOut { get; set; }
    public bool CancelRequested { get; set; }
    public string? CancelReason { get; set; }

    public ICollection<RunRepo> Repos { get; set; } = new List<RunRepo>();
    public ICollection<RunStep> Steps { get; set; } = new List<RunStep>();
    public ICollection<RunEvent> Events { get; set; } = new List<RunEvent>();
    public ICollection<RunDecision> Decisions { get; set; } = new List<RunDecision>();
    public ICollection<RunLlmCall> LlmCalls { get; set; } = new List<RunLlmCall>();
    public ICollection<RunArtifact> Artifacts { get; set; } = new List<RunArtifact>();
    public ICollection<RunSandbox> Sandboxes { get; set; } = new List<RunSandbox>();
}
