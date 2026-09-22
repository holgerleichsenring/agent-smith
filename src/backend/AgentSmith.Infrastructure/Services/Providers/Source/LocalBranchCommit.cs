using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-09-22-b6ad: a real commit on a real ref in a repository that has no remote, made WITHOUT
/// touching the operator's working tree or their index.
/// <para>
/// A bare file write would be the obvious implementation and it is the wrong one: the caller
/// records the commit sha as the pointer at the spec path, and a write that can answer no sha
/// leaves the pointer absent, which every later run reads as somebody else's edit. So this is git
/// plumbing through <see cref="LocalGitCommand"/> — the same "drive git as a process against the
/// operator's own checkout" shape <see cref="LocalBranchFastForward"/> already chose here, and for
/// the same reason: the branch that must move is not the one checked out.
/// </para>
/// <para>
/// <c>update-ref</c> is given the value it expects the ref to hold, so a ref that moved under us
/// is refused rather than rewound — the no-force rule the remote providers keep.
/// </para>
/// </summary>
public sealed class LocalBranchCommit
{
    private const string ZeroOid = "0000000000000000000000000000000000000000";

    public async Task<BranchWriteResult> WriteAsync(
        string repoPath, string branch, string defaultBranch, IReadOnlyList<RepoFile> files,
        string message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (!Directory.Exists(repoPath))
            return BranchWriteResult.Failed($"Local path not found: {repoPath}");
        if (files.Count == 0) return BranchWriteResult.Failed("no files to write");
        var index = Path.Combine(Path.GetTempPath(), $"agent-smith-index-{Guid.NewGuid():N}");
        try
        {
            return await CommitAsync(repoPath, branch, defaultBranch, files, message, index, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return BranchWriteResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
        finally
        {
            if (File.Exists(index)) File.Delete(index);
        }
    }

    private async Task<BranchWriteResult> CommitAsync(
        string repoPath, string branch, string defaultBranch, IReadOnlyList<RepoFile> files,
        string message, string index, CancellationToken ct)
    {
        var head = await RevParseAsync(repoPath, $"refs/heads/{branch}", ct);
        var parent = head ?? await RevParseAsync(repoPath, $"refs/heads/{defaultBranch}", ct);
        var seed = parent is null
            ? await Git(repoPath, index, null, ct, "read-tree", "--empty")
            : await Git(repoPath, index, null, ct, "read-tree", parent);
        if (seed.Exit != 0) return BranchWriteResult.Failed(seed.Reason("read-tree"));
        if (await StageAsync(repoPath, index, files, ct) is { } staging) return staging;

        var tree = await Git(repoPath, index, null, ct, "write-tree");
        if (tree.Exit != 0) return BranchWriteResult.Failed(tree.Reason("write-tree"));
        var commit = parent is null
            ? await Git(repoPath, index, message, ct, "commit-tree", tree.Out.Trim())
            : await Git(repoPath, index, message, ct, "commit-tree", tree.Out.Trim(), "-p", parent);
        if (commit.Exit != 0) return BranchWriteResult.Failed(commit.Reason("commit-tree"));

        var sha = commit.Out.Trim();
        var update = await Git(
            repoPath, index, null, ct, "update-ref", $"refs/heads/{branch}", sha, head ?? ZeroOid);
        return update.Exit == 0
            ? BranchWriteResult.Ok(sha)
            : BranchWriteResult.Failed(update.Reason("update-ref"));
    }

    /// <summary>Every file into the scratch index; the failure of any one of them, or null.</summary>
    private async Task<BranchWriteResult?> StageAsync(
        string repoPath, string index, IReadOnlyList<RepoFile> files, CancellationToken ct)
    {
        foreach (var file in files)
        {
            var blob = await Git(repoPath, index, file.Content, ct, "hash-object", "-w", "--stdin");
            if (blob.Exit != 0) return BranchWriteResult.Failed(blob.Reason("hash-object"));
            var add = await Git(
                repoPath, index, null, ct,
                "update-index", "--add", "--cacheinfo", $"100644,{blob.Out.Trim()},{file.Path}");
            if (add.Exit != 0) return BranchWriteResult.Failed(add.Reason("update-index"));
        }
        return null;
    }

    private async Task<string?> RevParseAsync(string repoPath, string reference, CancellationToken ct)
    {
        var result = await Git(repoPath, null, null, ct, "rev-parse", "--verify", "--quiet", reference);
        return result.Exit == 0 && result.Out.Trim() is { Length: > 0 } sha ? sha : null;
    }

    private static Task<LocalGitResult> Git(
        string repoPath, string? index, string? stdin, CancellationToken ct, params string[] args) =>
        LocalGitCommand.RunAsync(repoPath, index, stdin, ct, args);
}
