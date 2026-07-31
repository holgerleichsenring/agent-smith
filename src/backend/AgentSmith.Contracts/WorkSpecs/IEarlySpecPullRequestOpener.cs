using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: opens the draft PR AT THE SPEC COMMIT, not at the end of the run —
/// otherwise there is nothing to review while the run is still working, and the
/// reviewer's edit arrives too late to change the outcome. Accepted consequence:
/// a parked or not-implementable run now leaves a PR containing only the spec.
/// </summary>
public interface IEarlySpecPullRequestOpener
{
    Task<string?> OpenAsync(
        PipelineContext pipeline, RepoConnection carryingRepo, WorkSpecArtifact artifact,
        CancellationToken cancellationToken);
}
