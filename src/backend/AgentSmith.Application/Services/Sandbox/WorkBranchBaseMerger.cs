using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecExecution;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0496: brings a reused work branch up to date with the base it was cut from.
/// <para>
/// MERGE, never rebase — a pull request hangs off the branch, so rewriting its pushed
/// history would orphan the review. No fetch either: the sandbox clone is full, so
/// <c>origin/&lt;base&gt;</c> is already local, and the checkout step carries no
/// credentials a fetch would need.
/// </para>
/// <para>
/// 2026-09-13-5cdf: the base is HANDED IN, not resolved here. The work-branch cut resolved
/// it one step earlier, and a merger that asked again would merge the DEFAULT branch into a
/// slice cut from a feature rung — quietly dissolving the isolation the rung exists for.
/// </para>
/// <para>
/// A conflict is ABORTED here, before anyone sees the result: the finalizer tail stages
/// <c>git add -A</c> and force-pushes with a lease, and a tree carrying conflict markers
/// must never reach it.
/// </para>
/// <para>
/// 2026-11-26-462aa: the index files (done.md, MEMORY.md) merge with git's union driver, written into the clone's
/// info/attributes first — git reads attributes from the work branch's tree, which an older branch lacks. It replaces
/// the three index unions (2026-11-17-023fb, 2026-11-24-6811b) this merger ran on a conflict.
/// </para>
/// </summary>
public sealed class WorkBranchBaseMerger(
    BaseRefresh.BaseMergeResolutionCommit resolution, // 2026-11-24-6811c: what a resolver resolved is committed here
    ILogger<WorkBranchBaseMerger> logger)
{
    internal const int TimeoutSeconds = 300;

    /// <summary>Merges the resolved base into the sandbox's current HEAD.</summary>
    /// <param name="resolveRest">2026-11-24-6811c: given the paths git left unmerged, resolves them in the working
    /// tree; without one a conflict aborts (a rework's checkout). 2026-11-24-9dc0a: it answers what the server's check
    /// still finds open, and the merge is committed only when nothing is.</param>
    public async Task<BaseMergeResult> MergeIntoCurrentAsync(
        ISandbox sandbox, ResolvedBase resolved, CancellationToken ct,
        Func<IReadOnlyList<string>, CancellationToken, Task<BaseRefresh.BaseMergeResolution>>? resolveRest = null)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        if (resolved.Ref is not { } baseRef)
            return BaseMergeResult.Unavailable("this clone's origin/HEAD names no base branch");

        // Exit 0 = the base is already an ancestor of HEAD, 1 = it is not, anything else =
        // the ref cannot be read. Deciding on exit codes keeps the answer independent of
        // git's locale and version, which matching "Already up to date." would not be.
        var ancestor = await RunAsync(sandbox, ["merge-base", "--is-ancestor", baseRef, "HEAD"], ct);
        if (ancestor.ExitCode == 0) return BaseMergeResult.UpToDate(baseRef);
        if (ancestor.ExitCode != 1)
            return BaseMergeResult.Unavailable($"{baseRef} cannot be read in this sandbox");

        await UnionIndexesAsync(sandbox, ct);
        var merge = await RunAsync(sandbox, ["merge", "--no-edit", baseRef], ct);
        if (merge.ExitCode == 0)
        {
            logger.LogInformation("Merged {BaseRef} into the work branch", baseRef);
            return BaseMergeResult.Merged(baseRef);
        }
        return await AbortAsync(sandbox, baseRef, merge.ErrorMessage, resolveRest, ct);
    }

    private async Task<BaseMergeResult> AbortAsync(ISandbox sandbox, string baseRef, string? error,
        Func<IReadOnlyList<string>, CancellationToken, Task<BaseRefresh.BaseMergeResolution>>? resolveRest, CancellationToken ct)
    {
        var conflicting = await ConflictingPathsAsync(sandbox, ct);
        var resolved = conflicting.Count > 0 && resolveRest is not null
            ? await resolution.CommitAsync(sandbox, conflicting, resolveRest, ct) : null;
        if (resolved is { IsWhole: true })
        {
            logger.LogInformation("Merged {BaseRef}; {Count} conflicting path(s) resolved by a master", baseRef, conflicting.Count);
            return BaseMergeResult.Merged(baseRef) with { ResolvedByMaster = true };
        }
        var abort = await RunAsync(sandbox, ["merge", "--abort"], ct);
        if (abort.ExitCode != 0)
            logger.LogWarning("git merge --abort exited {Exit}: {Error}", abort.ExitCode, abort.ErrorMessage);
        if (conflicting.Count > 0)
        {
            // 2026-11-24-9dc0a: only what is still open after the master is named.
            var open = resolved?.Open is { Count: > 0 } left ? left : conflicting;
            logger.LogWarning("Merging {BaseRef} conflicts in {Count} path(s)", baseRef, open.Count);
            return BaseMergeResult.Conflicted(baseRef, open, resolved?.Reason);
        }
        // A merge that never started leaves the tree exactly as the checkout left it —
        // the state every run before this one worked in. Reported, not fatal.
        logger.LogWarning("git merge {BaseRef} did not run: {Error}", baseRef, error);
        return BaseMergeResult.Unavailable($"git merge {baseRef} did not run: {error}");
    }

    // A failure leaves the merge as it was before 462aa: an index line collision then reaches the master.
    private async Task UnionIndexesAsync(ISandbox sandbox, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(IndexMergeAttributes.CloneStep(TimeoutSeconds), progress: null, ct);
        if (result.ExitCode != 0)
            logger.LogWarning("The index union attributes were not written ({Exit}): {Error}", result.ExitCode, result.ErrorMessage);
    }

    private async Task<IReadOnlyList<string>> ConflictingPathsAsync(ISandbox sandbox, CancellationToken ct)
    {
        var result = await RunAsync(sandbox, ["-c", "core.quotePath=false", "diff", "--name-only", "--diff-filter=U"], ct); // 9dc0a
        if (result.ExitCode != 0) return [];
        return [.. (result.OutputContent ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    private static Task<StepResult> RunAsync(
        ISandbox sandbox, IReadOnlyList<string> args, CancellationToken ct) =>
        sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "git", Args: args,
                TimeoutSeconds: TimeoutSeconds),
            progress: null, ct);
}
