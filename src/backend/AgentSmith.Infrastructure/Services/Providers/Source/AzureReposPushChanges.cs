using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-23-c81ea: the changes one Azure Repos push carries, each decided by what is at the base commit. Azure Repos
/// refuses an add for a path that is there and an edit for one that is not, so a write is an ADD or an EDIT per path;
/// a deletion is a DELETE with the item path and no content.
/// <para>
/// Two probes for two questions. The add-or-edit probe reads any failure as absent, as it always did: a wrong guess
/// is refused by the host. The deletion probe reads ONLY the host's not-found as absent and throws otherwise, so a
/// transient error never drops a required deletion.
/// </para>
/// </summary>
public sealed class AzureReposPushChanges(string project, string repoName, ILogger logger)
{
    public async Task<List<GitChange>> ComposeAsync(
        GitHttpClient client, string baseSha, IReadOnlyList<RepoFile> files, CancellationToken ct)
    {
        var changes = new List<GitChange>(files.Count);
        foreach (var file in files)
            changes.Add(file.Delete
                ? new GitChange { ChangeType = VersionControlChangeType.Delete, Item = Item(file.Path) }
                : new GitChange
                {
                    ChangeType = await ExistsLenientAsync(client, baseSha, file.Path, ct)
                        ? VersionControlChangeType.Edit : VersionControlChangeType.Add,
                    Item = Item(file.Path),
                    NewContent = new ItemContent { Content = file.Content, ContentType = ItemContentType.RawText },
                });
        return changes;
    }

    /// <summary>Whether <paramref name="path"/> is at <paramref name="baseSha"/>: false only on the host's not-found.</summary>
    public async Task<bool> ExistsStrictAsync(GitHttpClient client, string baseSha, string path, CancellationToken ct)
    {
        try
        {
            return await GetAsync(client, baseSha, path, ct) is not null;
        }
        catch (VssServiceException ex) when (AzureReposBranchRead.IsNotFound(ex))
        {
            logger.LogDebug(ex, "{Project}/{Repo}: {Path} is absent at {Sha}", project, repoName, path, baseSha);
            return false;
        }
    }

    private async Task<bool> ExistsLenientAsync(GitHttpClient client, string baseSha, string path, CancellationToken ct)
    {
        try
        {
            return await GetAsync(client, baseSha, path, ct) is not null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "{Project}/{Repo}: {Path} read as absent at {Sha}, so it is added", project, repoName, path, baseSha);
            return false;
        }
    }

    private Task<GitItem> GetAsync(GitHttpClient client, string baseSha, string path, CancellationToken ct) =>
        client.GetItemAsync(
            project: project, repositoryId: repoName, path: path,
            versionDescriptor: new GitVersionDescriptor { Version = baseSha, VersionType = GitVersionType.Commit },
            cancellationToken: ct);

    private static GitItem Item(string path) => new() { Path = "/" + path.TrimStart('/') };
}
