using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Infrastructure.Services.Providers.Pipelines;

/// <summary>2026-11-17-3435a, shared by 2026-11-18-3863e: a GitLab pipeline's status as a check outcome — success and
/// skipped passed, failed and canceled failed, everything else (created, pending, running, manual, scheduled) pending.</summary>
public static class GitLabPipelineStatus
{
    public static CiCheckOutcome Outcome(string? status) => status switch
    {
        "success" or "skipped" => CiCheckOutcome.Passed,
        "failed" or "canceled" => CiCheckOutcome.Failed,
        _ => CiCheckOutcome.Pending,
    };
}
