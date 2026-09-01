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
/// </summary>
public sealed class DeliveryDiff(
    SandboxBaseBranch baseBranchReader,
    SandboxRunStartCommit runStartReader,
    ILogger<DeliveryDiff> logger)
{
    private const int GitTimeoutSeconds = 120;

    /// <param name="runId">
    /// The run taking the diff, so the ladder can find the commit it started from. Null
    /// leaves that rung out and the ladder behaves exactly as it did before.
    /// </param>
    public async Task<DiffResult> ForBranchAsync(
        ISandbox sandbox, string? runId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        var baseBranch = await baseBranchReader.ResolveAsync(sandbox, cancellationToken);
        var runStart = await runStartReader.ResolveAsync(sandbox, runId, cancellationToken);
        foreach (var (against, description) in Candidates(baseBranch, runStart))
        {
            var diff = await TryDiffAsync(sandbox, against, cancellationToken);
            if (diff is null) continue;
            logger.LogInformation(
                "Delivery diff taken {Description} ({Chars:N0} chars)", description, diff.Length);
            return new DiffResult(diff, description, BaseRef: BaseRefOf(against));
        }

        logger.LogWarning("No delivery diff could be taken — no comparable base ref in the sandbox");
        return new DiffResult(string.Empty, "no comparable base", Failed: true);
    }

    /// <summary>
    /// The comparison's ref, or null for the HEAD rung — HEAD is the branch, so a search of
    /// it would search the delivery under the name of the thing it is compared against.
    /// </summary>
    private static string? BaseRefOf(string[] against) =>
        against is ["HEAD"] ? null : against[0];

    // Ordered by how close each comparison is to "what this branch delivers".
    private static IEnumerable<(string[] Args, string Description)> Candidates(
        string? baseBranch, string? runStart)
    {
        if (!string.IsNullOrWhiteSpace(baseBranch))
        {
            yield return ([$"origin/{baseBranch}"], $"against origin/{baseBranch}");
            yield return ([baseBranch], $"against {baseBranch}");
        }
        // Everything this run put on the branch, committed and uncommitted alike — the
        // answer when the branch and the base are the same ref.
        if (!string.IsNullOrWhiteSpace(runStart))
            yield return ([runStart], $"against {runStart} (where this run started)");
        // The branch's own first parent: everything committed on it plus the working tree.
        yield return (["HEAD"], "against HEAD (uncommitted work only)");
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
