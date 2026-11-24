using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-23-c81ea: every entry of a local branch write into the SCRATCH index — a write as a blob added under its
/// path, a deletion as <c>update-index --force-remove</c>. Moved out of <see cref="LocalBranchCommit"/> when deletions
/// joined, unchanged for writes.
/// <para>2026-11-23-c81ed: a deletion is an <c>update-index --index-info</c> entry of mode 0 instead — the path form
/// needs a work tree, and a bare origin (a GitOps repo served from a path) has none. The object id it names is ignored
/// but must have the repository's hash length, so it is the empty blob's id.</para>
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
                if (await RemoveAsync(repoPath, index, file.Path, ct) is { } refused) return refused;
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

    private static async Task<BranchWriteResult?> RemoveAsync(string repoPath, string index, string path, CancellationToken ct)
    {
        var empty = await Git(repoPath, index, string.Empty, ct, "hash-object", "--stdin");
        if (empty.Exit != 0) return BranchWriteResult.Failed(empty.Reason("hash-object"));
        var remove = await Git(repoPath, index, $"0 {empty.Out.Trim()}\t{path}\n", ct, "update-index", "--index-info");
        return remove.Exit != 0 ? BranchWriteResult.Failed(remove.Reason("update-index")) : null;
    }

    private static Task<LocalGitResult> Git(
        string repoPath, string? index, string? stdin, CancellationToken ct, params string[] args) =>
        LocalGitCommand.RunAsync(repoPath, index, stdin, ct, args);
}
