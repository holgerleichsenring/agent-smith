using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-09-22-b6ad: one commit carrying every file, through the SAME DevOps git client and
/// personal access token the provider already reads a file with. A PUSH is Azure Repos' one call
/// that carries a ref update and a commit together, so the whole set lands as one commit and the
/// branch moves with it.
/// <para>
/// The ref update's <c>OldObjectId</c> is what makes this safe on both paths: for an existing
/// branch it is that branch's head, so a concurrent commit makes the server refuse the push
/// rather than rewind it; for an absent branch it is the default branch's head, which is the base
/// the new branch is cut at. 2026-11-23-c81ea: that refusal (TF401028, the stale old object id) is a
/// <see cref="BranchWriteResult.Conflict"/>.
/// </para>
/// </summary>
public sealed class AzureReposBranchWrite(
    string project, string repoName, IAzDoClientFactory clientFactory,
    string organizationUrl, string personalAccessToken, ILogger logger)
{
    private const string StaleRefCode = "TF401028";
    private readonly AzureReposPushChanges _changes = new(project, repoName, logger);
    private readonly AbsentDeletionFilter _deletions = new(logger);

    public async Task<BranchWriteResult> WriteAsync(
        string branch, string defaultBranch, IReadOnlyList<RepoFile> files, string message,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0) return BranchWriteResult.Failed("no files to write");
        try
        {
            var client = await clientFactory.CreateGitClientAsync(
                organizationUrl, personalAccessToken, cancellationToken);
            var baseSha = await HeadOfAsync(client, branch, cancellationToken)
                ?? await HeadOfAsync(client, defaultBranch, cancellationToken);
            if (baseSha is null)
                return BranchWriteResult.Failed(
                    $"neither '{branch}' nor the default branch '{defaultBranch}' has a head to commit on");
            var filtered = await _deletions.FilterAsync(
                files, (path, ct) => _changes.ExistsStrictAsync(client, baseSha, path, ct), cancellationToken);
            if (filtered.Refusal is { } refusal) return refusal;
            return filtered.WithNotes(await PushAsync(client, branch, baseSha, filtered.Files, message, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Project}/{Repo}: writing {Branch} failed", project, repoName, branch);
            return BranchWriteResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }

    private async Task<BranchWriteResult> PushAsync(
        GitHttpClient client, string branch, string baseSha, IReadOnlyList<RepoFile> files, string message,
        CancellationToken ct)
    {
        var push = new GitPush
        {
            RefUpdates = [new GitRefUpdate { Name = $"refs/heads/{branch}", OldObjectId = baseSha }],
            Commits = [new GitCommitRef { Comment = message, Changes = await _changes.ComposeAsync(client, baseSha, files, ct) }],
        };
        GitPush? pushed;
        try { pushed = await client.CreatePushAsync(push, project, repoName, cancellationToken: ct); }
        catch (VssServiceException ex) when (ex.Message.Contains(StaleRefCode, StringComparison.Ordinal))
        {
            logger.LogInformation(ex, "{Project}/{Repo}: {Branch} moved under the push", project, repoName, branch);
            return BranchWriteResult.Conflicted($"`{branch}` moved while the commit was made: {ex.Message.Split('\n', 2)[0].Trim()}");
        }
        var sha = pushed?.Commits?.FirstOrDefault()?.CommitId;
        if (string.IsNullOrWhiteSpace(sha))
            return BranchWriteResult.Failed("Azure Repos accepted the push but named no commit");
        logger.LogInformation(
            "{Project}/{Repo}: {Count} file(s) committed as {Sha} on {Branch}", project, repoName, files.Count, sha, branch);
        return BranchWriteResult.Ok(sha!);
    }

    private async Task<string?> HeadOfAsync(GitHttpClient client, string branch, CancellationToken ct)
    {
        var refs = await client.GetRefsAsync(
            project, repoName, filter: $"heads/{branch}", cancellationToken: ct);
        return refs?.FirstOrDefault(
            r => string.Equals(r.Name, $"refs/heads/{branch}", StringComparison.Ordinal))?.ObjectId;
    }
}
