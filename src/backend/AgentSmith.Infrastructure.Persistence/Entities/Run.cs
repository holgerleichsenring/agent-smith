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
    // p0330: the spawner's container/pod handle for a SPAWNED orchestrator run
    // (from RunStartedEvent.JobId). The cancel enforcer force-kills by this id.
    // Null for in-process runs.
    public string? JobId { get; set; }
    // p0330: the durable kill deadline. Set with CancelRequested by the cancel
    // endpoint (now + grace); the enforcer terminates any non-terminal run whose
    // deadline elapsed. Persisted so a server restart inside the grace window
    // still guarantees the kill — never an in-process timer.
    public DateTimeOffset? CancelDeadlineAt { get; set; }
    // p0344b: the run-story snapshot taken at run end (RunStoryRecordedEvent) —
    // camelCase wire JSON served verbatim on the run detail. ProgressLedgerJson
    // is the p0341 ledger ([{id,activity,status,target}]), AcceptanceJson the
    // ratified criteria + p0340 dispositions ({criteria,outcome,ratifiedBy}).
    // Null on pre-p0344b rows and on runs without a ledger / ratified contract.
    public string? ProgressLedgerJson { get; set; }
    public string? AcceptanceJson { get; set; }
    // p0347: the run's per-repo PR outcomes (PullRequestOutcomeEvent), upserted by
    // repo — camelCase wire JSON ([{repo,status,url,reason,openedAt}]) served on the
    // run detail and flattened by GET /api/pull-requests. Keeps EVERY repo's PR for a
    // multi-repo run (the single Run/RunRepo PrUrl is lossy). Null on pre-p0347 rows
    // and on runs that opened no PR.
    public string? PullRequestsJson { get; set; }
    // p0357: the run's resolved cost budget (RunBudgetResolvedEvent from ScopeRepos,
    // step ~4) — complexity tier + cap. The dashboard renders CostTotalUsd against
    // BudgetCapUsd as a spent/cap bar. Null on pre-p0357 rows and on runs whose
    // classifier returned Unknown (no cap sizing happened).
    public string? BudgetTier { get; set; }
    public decimal? BudgetCapUsd { get; set; }
    public long? BudgetCapTokens { get; set; }

    public ICollection<RunRepo> Repos { get; set; } = new List<RunRepo>();
    public ICollection<RunStep> Steps { get; set; } = new List<RunStep>();
    public ICollection<RunEvent> Events { get; set; } = new List<RunEvent>();
    public ICollection<RunDecision> Decisions { get; set; } = new List<RunDecision>();
    public ICollection<RunLlmCall> LlmCalls { get; set; } = new List<RunLlmCall>();
    public ICollection<RunArtifact> Artifacts { get; set; } = new List<RunArtifact>();
    public ICollection<RunSandbox> Sandboxes { get; set; } = new List<RunSandbox>();
}
