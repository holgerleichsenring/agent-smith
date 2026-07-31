using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: writes one revision to the ticket branch as ITS OWN commit, before any
/// source edit, and pushes it immediately — the reviewer must have something to
/// edit while the run is still working. Git is the UI: diff, blame, history and
/// the PR review need no new surface.
/// </summary>
public interface IWorkSpecWriter
{
    Task<WorkSpecWriteResult> WriteAsync(
        PipelineContext pipeline, RepoConnection carryingRepo, WorkSpecArtifact artifact,
        CancellationToken cancellationToken);
}
