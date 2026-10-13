using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// The two git reads the commit gate needs — which files the branch changed against its base,
/// and what a meta file said AT that base. Reading Limits and baseline from the base, never the
/// working tree, is what keeps the master from raising its own ceiling. Paths are NUL-separated
/// and kept raw, exactly as the baseline records them.
/// 2026-10-13-2d0c: '--' ends the revisions, so no path is read as an option or a ref; presence
/// is asked with ls-tree, so a git that cannot answer is a failure, never an absent file.
/// </summary>
public sealed class StructureGitReads(StructureGitRunner git, StructureZeroListing listing)
{
    /// <summary>Changed and untracked paths, deletions left out; null when git could not answer
    /// completely.</summary>
    public async Task<IReadOnlyList<string>?> ChangedPathsAsync(
        ISandbox sandbox, string baseRev, CancellationToken cancellationToken)
    {
        var changed = await OutputAsync(
            sandbox, ["diff", "-z", "--name-only", "--diff-filter=d", baseRev, "--"], cancellationToken);
        var untracked = await OutputAsync(
            sandbox, ["ls-files", "-z", "--others", "--exclude-standard", "--full-name"], cancellationToken);
        if (listing.Parse(changed) is not { } tracked || listing.Parse(untracked) is not { } added) return null;
        return [.. tracked.Concat(added).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>The file at <paramref name="baseRev"/>: its content, absent, or why git could not say.</summary>
    public async Task<StructureBaseRead> ReadAtAsync(
        ISandbox sandbox, string baseRev, string path, CancellationToken cancellationToken)
    {
        var listed = await git.RunAsync(sandbox, ["ls-tree", "--name-only", baseRev, "--", path], cancellationToken);
        if (listed.ExitCode != 0)
            return StructureBaseRead.Failed($"git ls-tree {baseRev} -- {path} exited {listed.ExitCode}");
        if (string.IsNullOrWhiteSpace(listed.OutputContent)) return StructureBaseRead.Absent;
        var shown = await git.RunAsync(sandbox, ["show", $"{baseRev}:{path}"], cancellationToken);
        return shown.ExitCode == 0
            ? new StructureBaseRead(shown.OutputContent ?? string.Empty)
            : StructureBaseRead.Failed($"git show {baseRev}:{path} exited {shown.ExitCode}");
    }

    private async Task<string?> OutputAsync(ISandbox sandbox, string[] args, CancellationToken ct)
    {
        var result = await git.RunAsync(sandbox, args, ct);
        return result.ExitCode == 0 ? result.OutputContent ?? string.Empty : null;
    }
}
