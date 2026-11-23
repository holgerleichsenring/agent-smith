using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

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
/// is refused rather than rewound — the no-force rule the remote providers keep. 2026-11-23-c81ea: that refusal
/// is a <see cref="BranchWriteResult.Conflict"/>, and a deletion is removed from the scratch index.
/// </para>
/// </summary>
public sealed class LocalBranchCommit(ILogger logger, LocalPathProbe probe)
{
    private const string ZeroOid = "0000000000000000000000000000000000000000";

    // 2026-11-23-c81ea: update-ref's words for a ref that moved ("is at X but expected Y") or appeared meanwhile.
    private static readonly string[] MovedMarkers = ["but expected", "reference already exists"];

    private readonly LocalIndexStaging _staging = new();
    private readonly AbsentDeletionFilter _deletions = new(logger);

    public LocalBranchCommit(ILogger logger) : this(logger, new LocalPathProbe()) { }

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
        var filtered = await _deletions.FilterAsync(
            files, (path, c) => probe.ExistsAtAsync(repoPath, parent, path, c), ct);
        if (filtered.Refusal is { } refusal) return refusal;
        var seed = parent is null
            ? await Git(repoPath, index, null, ct, "read-tree", "--empty")
            : await Git(repoPath, index, null, ct, "read-tree", parent);
        if (seed.Exit != 0) return BranchWriteResult.Failed(seed.Reason("read-tree"));
        if (await _staging.StageAsync(repoPath, index, filtered.Files, ct) is { } staging) return staging;

        var tree = await Git(repoPath, index, null, ct, "write-tree");
        if (tree.Exit != 0) return BranchWriteResult.Failed(tree.Reason("write-tree"));
        var commit = parent is null
            ? await Git(repoPath, index, message, ct, "commit-tree", tree.Out.Trim())
            : await Git(repoPath, index, message, ct, "commit-tree", tree.Out.Trim(), "-p", parent);
        if (commit.Exit != 0) return BranchWriteResult.Failed(commit.Reason("commit-tree"));

        var sha = commit.Out.Trim();
        var update = await Git(
            repoPath, index, null, ct, "update-ref", $"refs/heads/{branch}", sha, head ?? ZeroOid);
        if (update.Exit == 0) return filtered.WithNotes(BranchWriteResult.Ok(sha));
        return MovedMarkers.Any(m => update.Error.Contains(m, StringComparison.Ordinal))
            ? BranchWriteResult.Conflicted(update.Reason("update-ref"))
            : BranchWriteResult.Failed(update.Reason("update-ref"));
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
