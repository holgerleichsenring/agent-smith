using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-23-c81ea: every entry of a local branch write into the SCRATCH index — a write as a blob added under its
/// path, a deletion as <c>update-index --force-remove</c>. Moved out of <see cref="LocalBranchCommit"/> when deletions
/// joined, unchanged for writes.
/// </summary>
public sealed class LocalIndexStaging
{
    /// <summary>The failure of the first entry that could not be staged, or null.</summary>
    public async Task<BranchWriteResult?> StageAsync(
        string repoPath, string index, IReadOnlyList<RepoFile> files, CancellationToken ct)
    {
        foreach (var file in files)
        {
            if (file.Delete)
            {
                var remove = await Git(repoPath, index, null, ct, "update-index", "--force-remove", file.Path);
                if (remove.Exit != 0) return BranchWriteResult.Failed(remove.Reason("update-index"));
                continue;
            }
            var blob = await Git(repoPath, index, file.Content, ct, "hash-object", "-w", "--stdin");
            if (blob.Exit != 0) return BranchWriteResult.Failed(blob.Reason("hash-object"));
            var add = await Git(
                repoPath, index, null, ct,
                "update-index", "--add", "--cacheinfo", $"100644,{blob.Out.Trim()},{file.Path}");
            if (add.Exit != 0) return BranchWriteResult.Failed(add.Reason("update-index"));
        }
        return null;
    }

    private static Task<LocalGitResult> Git(
        string repoPath, string? index, string? stdin, CancellationToken ct, params string[] args) =>
        LocalGitCommand.RunAsync(repoPath, index, stdin, ct, args);
}
