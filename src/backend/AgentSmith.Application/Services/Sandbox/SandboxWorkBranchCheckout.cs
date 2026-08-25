using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Puts the run's branch under a freshly cloned sandbox: check out the branch's EXISTING
/// content on a re-run, or create it from the clone's default HEAD on a first run.
/// <para>
/// p0496: a re-used branch also takes its base with it. Three runs in a row aborted at
/// the bootstrap gate on a file that was sitting on the base branch — the work branch had
/// been cut before it landed, and nothing reconciled the two. A branch created here is
/// already at the base and takes no merge.
/// </para>
/// </summary>
public sealed class SandboxWorkBranchCheckout(
    WorkBranchBaseMerger merger, ILogger<SandboxWorkBranchCheckout> logger)
{
    /// <summary>
    /// Null when the sandbox is ready to be worked in; otherwise the reason the run must
    /// stop before anything reads or writes this tree.
    /// </summary>
    public async Task<string?> SwitchAsync(ISandbox sandbox, RunBranch? requested, CancellationToken ct)
    {
        if (requested is null) return null;
        var branch = requested.Name.Value;

        // `git checkout` on the branch we are already on is a harmless no-op, so the
        // switch is unconditional — CheckoutAsync only ECHOES the requested branch back.
        var existing = await sandbox.RunStepAsync(CheckoutStepFactory.BuildCheckoutStep(branch), null, ct);
        if (existing.ExitCode != 0) return await CreateAsync(sandbox, branch, ct);

        logger.LogInformation("git checkout {Branch} (existing)", branch);
        if (!requested.ComposedFromTicket)
        {
            logger.LogInformation(
                "{Branch} was handed to this run rather than composed from its ticket — its base is left alone",
                branch);
            return null;
        }
        return Describe(branch, await merger.MergeIntoCurrentAsync(sandbox, ct));
    }

    private async Task<string?> CreateAsync(ISandbox sandbox, string branch, CancellationToken ct)
    {
        var created = await sandbox.RunStepAsync(CheckoutStepFactory.BuildCreateBranchStep(branch), null, ct);
        if (created.ExitCode != 0)
            logger.LogWarning(
                "git checkout -b {Branch} failed (exit={Exit}): {Err}",
                branch, created.ExitCode, created.ErrorMessage);
        return null;
    }

    private string? Describe(string branch, BaseMergeResult merge)
    {
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
