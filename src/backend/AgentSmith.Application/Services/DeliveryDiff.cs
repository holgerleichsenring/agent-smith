using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0420: what this branch DELIVERS against the branch it will merge into — committed
/// work and uncommitted work together.
/// <para>
/// The old gate asked what the current run had staged, which is why a resumed branch
/// carrying a complete delivery was recorded as FAILED (run c96d). Delivery is a
/// property of the branch, so the diff has to be too: everything the branch changed,
/// whoever changed it and in which run.
/// </para>
/// <para>
/// The base ref is not always present — a shallow clone has one branch and no other
/// ref to compare against. Rather than guess, this walks a declared chain and REPORTS
/// which comparison it managed; an account taken against an unknown base would be a
/// confident answer to a question nobody asked.
/// </para>
/// <para>
/// 2026-09-01-b467: and the chain knows where the RUN began, because the phase commits its
/// work before the gate reads it. On a branch that IS the base — a local repository with no
/// remote, which is what the bundled demo produces — the base rungs see nothing and HEAD
/// means uncommitted work only, so a correct delivery was invisible at every rung.
/// </para>
/// <para>
/// 2026-09-13-5cdf: the base comes from the same ladder the work-branch cut used. A slice
/// cut from a feature rung and accounted against <c>origin/HEAD</c> would report its
/// predecessors' work as its own delivery — to the keystone, to the acceptance account and
/// to the pull-request body.
/// </para>
/// </summary>
public sealed class DeliveryDiff(
    SandboxBaseLadder baseLadder,
    SandboxRunStartCommit runStartReader,
    ILogger<DeliveryDiff> logger)
{
    private const int GitTimeoutSeconds = 120;

    /// <param name="basis">
    /// The run taking the diff, so the ladder can find the commit it started from, and the
    /// parent stamp that names the rung this branch was cut from.
    /// </param>
    public async Task<DiffResult> ForBranchAsync(
        ISandbox sandbox, DeliveryBasis basis, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(basis);
        var resolved = await baseLadder.ResolveAsync(sandbox, basis.ParentTicketId, cancellationToken);
        var runStart = await runStartReader.ResolveAsync(sandbox, basis.RunId, cancellationToken);
        foreach (var (against, description) in DeliveryDiffCandidates.Ordered(resolved.Name, runStart))
        {
            var diff = await TryDiffAsync(sandbox, against, cancellationToken);
            if (diff is null) continue;
            logger.LogInformation(
                "Delivery diff taken {Description} ({Chars:N0} chars)", description, diff.Length);
            return new DiffResult(
                diff, description, BaseRef: DeliveryDiffCandidates.BaseRefOf(against));
        }

        logger.LogWarning("No delivery diff could be taken — no comparable base ref in the sandbox");
        return new DiffResult(string.Empty, "no comparable base", Failed: true);
    }

    private async Task<string?> TryDiffAsync(
        ISandbox sandbox, string[] against, CancellationToken cancellationToken)
    {
        var args = new List<string> { "diff", "--no-color" };
        args.AddRange(against);
        var result = await sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "git", Args: args, WorkingDirectory: "/work",
                TimeoutSeconds: GitTimeoutSeconds),
            progress: null, cancellationToken);
        return result.ExitCode == 0 ? result.OutputContent ?? string.Empty : null;
    }

    /// <summary>
    /// Does this diff change SOURCE, or only the run's own record? A branch whose entire
    /// diff is .agentsmith/ bookkeeping has delivered nothing a build can be green about
    /// and nothing a criterion can be satisfied by.
    /// </summary>
    public static bool CarriesSource(string diff) =>
        Specs.CitedFileIndex.FromDiff(diff).Paths.Any(path => !RunRecordPaths.IsRunRecordPath(path));

    /// <param name="Basis">How the comparison was taken — it belongs in the account.</param>
    /// <summary>
    /// 2026-08-25-0eae: <paramref name="BaseRef"/> is the ref the comparison actually ran
    /// against, or null when the ladder fell through to the branch itself. The account's
    /// search needs the ref, not the sentence describing it — and it needs THIS one, because
    /// resolving a base a second time could hand it a ref the diff it is reading was never
    /// taken against.
    /// </summary>
    public sealed record DiffResult(
        string Text, string Basis, bool Failed = false, string? BaseRef = null);
}
