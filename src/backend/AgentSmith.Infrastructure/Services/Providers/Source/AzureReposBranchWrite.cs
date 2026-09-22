using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;

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
/// the new branch is cut at.
/// </para>
/// <para>
/// Each change is an ADD or an EDIT depending on whether that path already exists at the base —
/// Azure Repos refuses an add for a path that is there and an edit for one that is not.
/// </para>
/// </summary>
public sealed class AzureReposBranchWrite(
    string project, string repoName, IAzDoClientFactory clientFactory,
    string organizationUrl, string personalAccessToken, ILogger logger)
{
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
            var push = await client.CreatePushAsync(
                await ComposeAsync(client, branch, baseSha, files, message, cancellationToken),
                project, repoName, cancellationToken: cancellationToken);
            var sha = push?.Commits?.FirstOrDefault()?.CommitId;
            if (string.IsNullOrWhiteSpace(sha))
                return BranchWriteResult.Failed("Azure Repos accepted the push but named no commit");
            logger.LogInformation(
                "{Project}/{Repo}: {Count} file(s) committed as {Sha} on {Branch}",
                project, repoName, files.Count, sha, branch);
            return BranchWriteResult.Ok(sha!);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Project}/{Repo}: writing {Branch} failed", project, repoName, branch);
            return BranchWriteResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }

    private async Task<GitPush> ComposeAsync(
        GitHttpClient client, string branch, string baseSha, IReadOnlyList<RepoFile> files,
        string message, CancellationToken ct)
    {
        var changes = new List<GitChange>(files.Count);
        foreach (var file in files)
            changes.Add(new GitChange
            {
                ChangeType = await ExistsAsync(client, baseSha, file.Path, ct)
                    ? VersionControlChangeType.Edit : VersionControlChangeType.Add,
                Item = new GitItem { Path = "/" + file.Path.TrimStart('/') },
                NewContent = new ItemContent
                {
                    Content = file.Content,
                    ContentType = ItemContentType.RawText,
                },
            });
        return new GitPush
        {
            RefUpdates =
            [
                new GitRefUpdate { Name = $"refs/heads/{branch}", OldObjectId = baseSha },
            ],
            Commits = [new GitCommitRef { Comment = message, Changes = changes }],
        };
    }

    private async Task<string?> HeadOfAsync(GitHttpClient client, string branch, CancellationToken ct)
    {
        var refs = await client.GetRefsAsync(
            project, repoName, filter: $"heads/{branch}", cancellationToken: ct);
        return refs?.FirstOrDefault(
            r => string.Equals(r.Name, $"refs/heads/{branch}", StringComparison.Ordinal))?.ObjectId;
    }

    private async Task<bool> ExistsAsync(
        GitHttpClient client, string baseSha, string path, CancellationToken ct)
    {
        try
        {
            var item = await client.GetItemAsync(
                project: project,
                repositoryId: repoName,
                path: path,
                versionDescriptor: new GitVersionDescriptor
                {
                    Version = baseSha,
                    VersionType = GitVersionType.Commit,
                },
                cancellationToken: ct);
            return item is not null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return false;
        }
    }
}
