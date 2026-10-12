using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// The two git reads the commit gate needs — which files the branch changed against its base,
/// and what a meta file said AT that base. Reading Limits and baseline from the base, never the
/// working tree, is what keeps the master from raising its own ceiling. Paths are NUL-separated
/// and kept raw, exactly as the baseline records them.
/// </summary>
public sealed class StructureGitReads
{
    private const int GitTimeoutSeconds = 60;

    /// <summary>Changed and untracked paths, deletions left out; null when git could not answer
    /// completely.</summary>
    public async Task<IReadOnlyList<string>?> ChangedPathsAsync(
        ISandbox sandbox, string baseRef, CancellationToken cancellationToken)
    {
        var changed = await RunAsync(
            sandbox, ["diff", "-z", "--name-only", "--diff-filter=d", baseRef], cancellationToken);
        var untracked = await RunAsync(
            sandbox, ["ls-files", "-z", "--others", "--exclude-standard", "--full-name"], cancellationToken);
        if (Paths(changed) is not { } tracked || Paths(untracked) is not { } added) return null;
        return [.. tracked.Concat(added).Distinct(StringComparer.Ordinal)];
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

    // The agent appends a newline to the one -z line; a listing at the capture cap may be cut.
    private static IReadOnlyList<string>? Paths(string? output)
    {
        if (output is null) return null;
        var raw = output.TrimEnd('\n');
        if (raw.Length >= StructureSourceListing.CaptureCap) return null;
        return raw.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}
