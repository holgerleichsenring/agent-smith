using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pocs.Build;

/// <summary>
/// 2026-11-23-933dd: what an earlier run already settled for the build step, or null when this run builds — a built
/// revision keeps its digest (and publishes it again), one blocked at the image stage stays blocked unless the run is
/// an overrule, and only a revision that passed the code scan (validated or overruled) is built.
/// </summary>
public static class PocBuildSettled
{
    public static CommandResult? Of(PocScanSubject subject, bool overrule, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(pipeline);
        if (subject.RevisionState == PocRevisionStates.Built && subject.Digest is { } digest)
        {
            pipeline.Set(ContextKeys.PocImageDigest, digest);
            return CommandResult.Ok($"Revision {subject.Revision} of '{subject.PocName}' was built earlier: {digest}.");
        }
        return subject switch
        {
            { RevisionState: PocRevisionStates.Blocked, BlockedStage: PocScanStages.Image } when !overrule => CommandResult.Fail(
                $"Revision {subject.Revision} of '{subject.PocName}' was blocked by the image scan earlier; it is not built again."),
            { RevisionState: PocRevisionStates.Blocked, BlockedStage: PocScanStages.Image } => null,
            { RevisionState: PocRevisionStates.Validated or PocRevisionStates.Overruled } => null,
            _ => CommandResult.Fail($"Revision {subject.Revision} of '{subject.PocName}' is {subject.RevisionState}; only a revision "
                + "that passed the code scan is built."),
        };
    }
}
