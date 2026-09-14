using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Puts the run's branch under a freshly cloned sandbox: check out the branch's EXISTING
/// content on a re-run, or create it from the resolved base on a first run.
/// <para>
/// p0496: a re-used branch also takes its base with it. Three runs in a row aborted at
/// the bootstrap gate on a file that was sitting on the base branch — the work branch had
/// been cut before it landed, and nothing reconciled the two. A branch created here is
/// already at the base and takes no merge.
/// </para>
/// <para>
/// 2026-09-13-5cdf: the base is RESOLVED, once, and the same answer serves both halves —
/// the rung is checked out before <c>git checkout -b</c>, which until now cut from
/// whatever HEAD the clone happened to be on, and the same resolved base is what a reused
/// branch merges. Until 2026-09-13-35a4 publishes a rung the ladder always falls through,
/// so this cuts and merges exactly where it did before.
/// </para>
/// </summary>
public sealed class SandboxWorkBranchCheckout(
    SandboxBaseLadder baseLadder,
    WorkBranchBaseMerger merger,
    ILogger<SandboxWorkBranchCheckout> logger)
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
        if (existing.ExitCode != 0) return await CreateAsync(sandbox, requested, ct);

        logger.LogInformation("git checkout {Branch} (existing)", branch);
        if (!requested.ComposedFromTicket)
        {
            logger.LogInformation(
                "{Branch} was handed to this run rather than composed from its ticket — its base is left alone",
                branch);
            return null;
        }
        var resolved = await baseLadder.ResolveAsync(sandbox, requested.ParentTicketId, ct);
        return Describe(branch, await merger.MergeIntoCurrentAsync(sandbox, resolved, ct));
    }

    private async Task<string?> CreateAsync(ISandbox sandbox, RunBranch requested, CancellationToken ct)
    {
        var branch = requested.Name.Value;
        var resolved = await baseLadder.ResolveAsync(sandbox, requested.ParentTicketId, ct);
        // A ladder that FELL THROUGH names the clone's own base, which is where a fresh
        // clone's HEAD already stands — checking it out would be a no-op dressed as a
        // decision, and on a tree some other step positioned it would silently move the
        // cut. Only a rung the ladder actually found is checked out.
        if (!resolved.FellThrough && resolved.Ref is { } rung)
        {
            var onto = await sandbox.RunStepAsync(CheckoutStepFactory.BuildCheckoutStep(rung), null, ct);
            if (onto.ExitCode != 0)
                return $"'{rung}' is this branch's base but could not be checked out "
                       + $"(exit={onto.ExitCode}): {onto.ErrorMessage}. Cutting '{branch}' from "
                       + "the clone's own base would deliver the whole feature instead of this slice.";
            logger.LogInformation("{Branch} is cut from {Rung}", branch, rung);
        }

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
