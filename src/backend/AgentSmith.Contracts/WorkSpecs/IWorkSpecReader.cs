using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: reads the last revision from the CHECKED-OUT ticket branch. The clone
/// is the fetch — a re-trigger or a PR-comment trigger lands on the same
/// <c>agent-smith/&lt;ticketId&gt;</c> branch, so whatever a reviewer committed on the
/// spec path is already in the working tree and is INPUT to the next revision.
/// </summary>
public interface IWorkSpecReader
{
    Task<WorkSpecReadResult?> ReadAsync(
        PipelineContext pipeline, RepoConnection carryingRepo, WorkSpecKey key,
        CancellationToken cancellationToken);
}
