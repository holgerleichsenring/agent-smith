using AgentSmith.Application.Models;
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
/// A conflict is ABORTED here, before anyone sees the result: the finalizer tail stages
/// <c>git add -A</c> and force-pushes with a lease, and a tree carrying conflict markers
/// must never reach it.
/// </para>
/// </summary>
public sealed class WorkBranchBaseMerger(
    SandboxBaseBranch baseBranch, ILogger<WorkBranchBaseMerger> logger)
{
    private const int TimeoutSeconds = 300;

    /// <summary>Merges the repository's base branch into the sandbox's current HEAD.</summary>
    public async Task<BaseMergeResult> MergeIntoCurrentAsync(ISandbox sandbox, CancellationToken ct)
    {
        var name = await baseBranch.ResolveAsync(sandbox, ct);
        if (name is null)
            return BaseMergeResult.Unavailable("this clone's origin/HEAD names no base branch");
        var baseRef = $"origin/{name}";

        // Exit 0 = the base is already an ancestor of HEAD, 1 = it is not, anything else =
        // the ref cannot be read. Deciding on exit codes keeps the answer independent of
        // git's locale and version, which matching "Already up to date." would not be.
        var ancestor = await RunAsync(sandbox, ["merge-base", "--is-ancestor", baseRef, "HEAD"], ct);
        if (ancestor.ExitCode == 0) return BaseMergeResult.UpToDate(baseRef);
        if (ancestor.ExitCode != 1)
            return BaseMergeResult.Unavailable($"{baseRef} cannot be read in this sandbox");

        var merge = await RunAsync(sandbox, ["merge", "--no-edit", baseRef], ct);
        if (merge.ExitCode == 0)
        {
            logger.LogInformation("Merged {BaseRef} into the work branch", baseRef);
            return BaseMergeResult.Merged(baseRef);
        }
        return await AbortAsync(sandbox, baseRef, merge.ErrorMessage, ct);
    }

    private async Task<BaseMergeResult> AbortAsync(
        ISandbox sandbox, string baseRef, string? error, CancellationToken ct)
    {
        var conflicting = await ConflictingPathsAsync(sandbox, ct);
        var abort = await RunAsync(sandbox, ["merge", "--abort"], ct);
        if (abort.ExitCode != 0)
            logger.LogWarning("git merge --abort exited {Exit}: {Error}", abort.ExitCode, abort.ErrorMessage);
        if (conflicting.Count > 0)
        {
            logger.LogWarning("Merging {BaseRef} conflicts in {Count} path(s)", baseRef, conflicting.Count);
            return BaseMergeResult.Conflicted(baseRef, conflicting);
        }
        // A merge that never started leaves the tree exactly as the checkout left it —
        // the state every run before this one worked in. Reported, not fatal.
        logger.LogWarning("git merge {BaseRef} did not run: {Error}", baseRef, error);
        return BaseMergeResult.Unavailable($"git merge {baseRef} did not run: {error}");
    }

    private async Task<IReadOnlyList<string>> ConflictingPathsAsync(ISandbox sandbox, CancellationToken ct)
    {
        var result = await RunAsync(sandbox, ["diff", "--name-only", "--diff-filter=U"], ct);
        if (result.ExitCode != 0) return [];
        return [.. (result.OutputContent ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    private static Task<StepResult> RunAsync(
        ISandbox sandbox, IReadOnlyList<string> args, CancellationToken ct) =>
        sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "git", Args: args,
                WorkingDirectory: Repository.SandboxWorkPath, TimeoutSeconds: TimeoutSeconds),
            progress: null, ct);
}
