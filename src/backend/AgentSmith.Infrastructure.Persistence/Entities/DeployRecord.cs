namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-11-18-3863f: one deploy run's delivery, followed after the run ends — inserted from its DeployDeliveredEvent,
/// then written only by the deploy follow. Keyed by the run; a pull request (<see cref="PullRequestUrl"/>) or a pipeline
/// run (<see cref="PipelineRunId"/>). Times are UTC ticks, as on the pull-request records, so SQLite compares them.
/// </summary>
public sealed class DeployRecord : EntityBase
{
    public string RunId { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string? ContextRepo { get; set; }
    public string Environment { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Delivery { get; set; } = string.Empty;

    /// <summary>The configured name of the repository the delivery went to.</summary>
    public string Repo { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
    public string? PullRequestUrl { get; set; }
    public string? PipelineRunId { get; set; }

    /// <summary>The pull request's state (open, merged, closed); null for a pipeline or before the first read.</summary>
    public string? PullRequestState { get; set; }

    /// <summary>One of CiStates: the pull request's CI, or the pipeline run's state.</summary>
    public string CiState { get; set; } = string.Empty;

    public string? CiFailuresJson { get; set; }
    public long DeliveredAtTicks { get; set; }
    public long? CheckedAtTicks { get; set; }
    public long? SettledAtTicks { get; set; }
    public string? Note { get; set; }

    /// <summary>2026-11-18-3863h: the environments deployed once this one settles green, comma-separated; null for none.</summary>
    public string? PromotesTo { get; set; }
}
