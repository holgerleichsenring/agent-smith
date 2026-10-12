using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565d: the two git reads the commit gate needs — which files the branch changed
/// against its base, and what a meta file said AT that base. Reading Limits and baseline from
/// the base, never the working tree, is what keeps the master from raising its own ceiling.
/// </summary>
public sealed class StructureGitReads
{
    private const int GitTimeoutSeconds = 60;

    /// <summary>Changed and untracked paths, deletions left out; null when git could not answer.</summary>
    public async Task<IReadOnlyList<string>?> ChangedPathsAsync(
        ISandbox sandbox, string baseRef, CancellationToken cancellationToken)
    {
        var changed = await RunAsync(sandbox, ["diff", "--name-only", "--diff-filter=d", baseRef], cancellationToken);
        var untracked = await RunAsync(sandbox, ["ls-files", "--others", "--exclude-standard"], cancellationToken);
        if (changed is null || untracked is null) return null;
        return [.. Lines(changed).Concat(Lines(untracked)).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>The file's content at <paramref name="baseRef"/>, or null when it did not exist there.</summary>
    public Task<string?> ShowAsync(
        ISandbox sandbox, string baseRef, string path, CancellationToken cancellationToken) =>
        RunAsync(sandbox, ["show", $"{baseRef}:{path}"], cancellationToken);

    private static async Task<string?> RunAsync(ISandbox sandbox, string[] args, CancellationToken ct)
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "git", Args: args,
            WorkingDirectory: Repository.SandboxWorkPath, TimeoutSeconds: GitTimeoutSeconds);
        var result = await sandbox.RunStepAsync(step, null, ct);
        return result.ExitCode == 0 ? result.OutputContent ?? string.Empty : null;
    }

    private static IEnumerable<string> Lines(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
