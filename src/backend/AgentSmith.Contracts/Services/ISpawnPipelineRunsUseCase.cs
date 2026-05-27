using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Single entry point that turns a (project, pipeline, envelope, matched-trigger) tuple
/// into N PipelineRequest enqueues — one per repo in project.Repos. All N requests
/// share Platform + TicketId; they differ only in RepoName. Submission goes through
/// ITicketClaimService.ClaimSpawnAsync (one lock + one lifecycle transition + N enqueues)
/// so dedup protections stay intact for multi-repo fan-out.
/// </summary>
public interface ISpawnPipelineRunsUseCase
{
    Task<SpawnResult> ExecuteAsync(
        AgentSmithConfig config,
        ResolvedProject project,
        string pipelineName,
        IncomingTicketEnvelope envelope,
        WebhookTriggerConfig matchedTrigger,
        CancellationToken cancellationToken,
        Dictionary<string, string>? planAnswers = null);
}
