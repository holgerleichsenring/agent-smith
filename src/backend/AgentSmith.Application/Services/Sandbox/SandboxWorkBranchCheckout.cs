using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
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
/// branch merges.
/// </para>
/// <para>
/// 2026-09-13-35a4: that base now comes from the PUBLISHER, not straight from the ladder,
/// so the rung a slice cuts from is created by whichever slice reaches this repository
/// first. Asking the ladder here instead would mean every slice of a feature fell through
/// to the clone's own base until somebody created the branch by hand. The publisher is
/// also why <see cref="RepoConnection"/> is threaded in: a push needs the platform token,
/// and nothing on this path carried one.
/// </para>
/// </summary>
public sealed class SandboxWorkBranchCheckout(
    SandboxRungPublisher rungs,
    WorkBranchBaseMerger merger,
    WorkBranchBaseMergeReport report,
    ILogger<SandboxWorkBranchCheckout> logger)
{
    /// <summary>
    /// The reason the run must stop, or the rung this branch was cut from — see
    /// <see cref="WorkBranchPlacement"/> for why the rung travels back out.
    /// </summary>
    public async Task<WorkBranchPlacement> SwitchAsync(
        ISandbox sandbox, RepoConnection config, RunBranch? requested, CancellationToken ct)
    {
        if (requested is null) return WorkBranchPlacement.On(null);
        var branch = requested.Name.Value;

        // `git checkout` on the branch we are already on is a harmless no-op, so the
        // switch is unconditional — CheckoutAsync only ECHOES the requested branch back.
        var existing = await sandbox.RunStepAsync(CheckoutStepFactory.BuildCheckoutStep(branch), null, ct);
        if (existing.ExitCode != 0) return await CreateAsync(sandbox, config, requested, ct);

        logger.LogInformation("git checkout {Branch} (existing)", branch);
        if (!requested.ComposedFromTicket)
        {
            logger.LogInformation(
                "{Branch} was handed to this run rather than composed from its ticket — its base is left alone",
                branch);
            return WorkBranchPlacement.On(null);
        }
        var resolved = await rungs.EnsureAsync(sandbox, config, requested.ParentTicketId, ct);
        var problem = report.Describe(branch, await merger.MergeIntoCurrentAsync(sandbox, resolved, ct));
        return problem is null ? Placed(resolved) : WorkBranchPlacement.Stop(problem);
    }

    // A ladder that fell through names the clone's own base — nobody CHOSE it, so it is
    // not a pull-request target; the provider's own default-branch lookup answers better.
    private static WorkBranchPlacement Placed(ResolvedBase resolved) =>
        WorkBranchPlacement.On(resolved.FellThrough ? null : resolved.Name);

    private async Task<WorkBranchPlacement> CreateAsync(
        ISandbox sandbox, RepoConnection config, RunBranch requested, CancellationToken ct)
    {
        var branch = requested.Name.Value;
        var resolved = await rungs.EnsureAsync(sandbox, config, requested.ParentTicketId, ct);
        // A ladder that FELL THROUGH names the clone's own base, which is where a fresh
        // clone's HEAD already stands — checking it out would be a no-op dressed as a
        // decision, and on a tree some other step positioned it would silently move the
        // cut. Only a rung the ladder actually found is checked out.
        if (!resolved.FellThrough && resolved.Ref is { } rung)
        {
            var onto = await sandbox.RunStepAsync(CheckoutStepFactory.BuildCheckoutStep(rung), null, ct);
            if (onto.ExitCode != 0)
                return WorkBranchPlacement.Stop(
                    $"'{rung}' is this branch's base but could not be checked out "
                    + $"(exit={onto.ExitCode}): {onto.ErrorMessage}. Cutting '{branch}' from "
                    + "the clone's own base would deliver the whole feature instead of this slice.");
            logger.LogInformation("{Branch} is cut from {Rung}", branch, rung);
        }

        var created = await sandbox.RunStepAsync(CheckoutStepFactory.BuildCreateBranchStep(branch), null, ct);
        if (created.ExitCode != 0)
            logger.LogWarning(
                "git checkout -b {Branch} failed (exit={Exit}): {Err}",
                branch, created.ExitCode, created.ErrorMessage);
        return Placed(resolved);
    }
}
