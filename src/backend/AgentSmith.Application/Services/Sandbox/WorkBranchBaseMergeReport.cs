using AgentSmith.Application.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Turns a base merge's outcome into the one thing the caller must decide: run on, or stop
/// before anything reads this tree.
/// <para>
/// 2026-09-13-35a4 extracted it from <see cref="SandboxWorkBranchCheckout"/>, which took on
/// publishing the feature branch and had no room left. Reporting a merge to an operator is
/// its own responsibility anyway: the checkout decides what to merge, this says what came
/// of it, and only a CONFLICT is a reason not to continue — an unavailable base leaves the
/// tree exactly as the checkout left it, which is where every run before p0496 worked.
/// </para>
/// </summary>
public sealed class WorkBranchBaseMergeReport(ILogger<WorkBranchBaseMergeReport> logger)
{
    /// <summary>Null when the run may continue; otherwise the reason it must not.</summary>
    public string? Describe(string branch, BaseMergeResult merge)
    {
        ArgumentNullException.ThrowIfNull(merge);
        switch (merge.Status)
        {
            case BaseMergeStatus.Merged:
                logger.LogInformation("{Branch} now carries {BaseRef}", branch, merge.BaseRef);
                return null;
            case BaseMergeStatus.UpToDate:
                logger.LogInformation("{Branch} already carries {BaseRef}", branch, merge.BaseRef);
                return null;
            case BaseMergeStatus.Conflicted:
                return $"merging '{merge.BaseRef}' into '{branch}' conflicts in "
                       + $"{merge.ConflictingPaths.Count} path(s): {string.Join(", ", merge.ConflictingPaths)}. "
                       + "The merge was aborted, so the branch is unchanged — resolve the conflict on "
                       + $"'{branch}', or delete it to start again from '{merge.BaseRef}'.";
            default:
                logger.LogWarning(
                    "{Branch} keeps the base it was cut from: {Reason}", branch, merge.Reason);
                return null;
        }
    }
}
