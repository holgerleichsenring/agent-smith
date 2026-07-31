using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: commits the revision, records the pointer, opens the draft PR at that
/// commit and publishes the revision onto the run context. Split from the
/// derivation handler because "produce the next revision" and "make it visible"
/// are two reasons to change.
/// </summary>
public interface IWorkSpecPublisher
{
    Task<CommandResult> PublishAsync(
        PipelineContext pipeline,
        string project,
        RepoConnection carryingRepo,
        WorkSpecArtifact artifact,
        IReadOnlyList<IgnoredInstruction> ignoredInstructions,
        CancellationToken cancellationToken);
}
