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
/// </summary>
public sealed class DeliveryDiff(ILogger<DeliveryDiff> logger)
{
    private const int GitTimeoutSeconds = 120;

    public async Task<DiffResult> ForBranchAsync(
        ISandbox sandbox, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        var baseBranch = await RemoteHeadAsync(sandbox, cancellationToken);
        foreach (var (against, description) in Candidates(baseBranch))
        {
            var diff = await TryDiffAsync(sandbox, against, cancellationToken);
            if (diff is null) continue;
            logger.LogInformation(
                "Delivery diff taken {Description} ({Chars:N0} chars)", description, diff.Length);
            return new DiffResult(diff, description);
        }

        logger.LogWarning("No delivery diff could be taken — no comparable base ref in the sandbox");
        return new DiffResult(string.Empty, "no comparable base", Failed: true);
    }

    /// <summary>
    /// The branch the clone's origin points at — the repository's own answer to "what do
    /// you merge into". Nothing earlier in the run carries it, and asking the repo beats
    /// hard-coding a name that is right for most projects and wrong for this one.
    /// </summary>
    private async Task<string?> RemoteHeadAsync(ISandbox sandbox, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "git", Args: ["symbolic-ref", "--short", "refs/remotes/origin/HEAD"],
                WorkingDirectory: "/work", TimeoutSeconds: GitTimeoutSeconds),
            progress: null, ct);
        if (result.ExitCode != 0) return null;
        var head = (result.OutputContent ?? string.Empty).Trim();
        return head.StartsWith("origin/", StringComparison.Ordinal) ? head["origin/".Length..] : null;
    }

    // Ordered by how close each comparison is to "what this branch delivers".
    private static IEnumerable<(string[] Args, string Description)> Candidates(string? baseBranch)
    {
        if (!string.IsNullOrWhiteSpace(baseBranch))
        {
            yield return ([$"origin/{baseBranch}"], $"against origin/{baseBranch}");
            yield return ([baseBranch], $"against {baseBranch}");
        }
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

    /// <param name="Basis">How the comparison was taken — it belongs in the account.</param>
    public sealed record DiffResult(string Text, string Basis, bool Failed = false);
}
