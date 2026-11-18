using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-18-3863e: starts a host's pipeline and reads a started run back — GitHub Actions workflow_dispatch, an Azure
/// DevOps pipeline run, a GitLab pipeline. Made per repository by <see cref="IPipelineRunnerFactory"/>; the repository is
/// the one the pipeline is defined in.
/// </summary>
public interface IPipelineRunner
{
    /// <summary>Starts the pipeline; a refusal by the host throws with the host's own words.</summary>
    Task<PipelineRunStart> StartAsync(PipelineRunOrder order, CancellationToken cancellationToken);

    /// <summary>The run's state as one of <see cref="CiStates"/>, with the failed jobs of a red one, bounded by
    /// <paramref name="bounds"/> (its HeadSha is not used).</summary>
    Task<PipelineRunState> ReadAsync(string runId, CiReadRequest bounds, CancellationToken cancellationToken);
}
