using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-23-c81ea: every file under one folder of a branch, read from the FOLDER'S OWN tree. The parent directory's
/// listing names the folder's tree sha, and a recursive read of that tree is bounded by the folder: a recursive read at
/// the branch head would read the whole repository on every call and hit GitHub's truncation on a large one.
/// </summary>
public sealed class GitHubFolderFiles(
    GitHubRepoAddress address, IGitHubClientFactory clientFactory, string token, ILogger logger)
{
    public async Task<RepoFileListing> ListAsync(string path, string branch, int maxFiles, CancellationToken ct)
    {
        _ = ct;
        var folder = path.Replace('\\', '/').Trim('/');
        var client = clientFactory.Create(address, token);
        var treeSha = await FolderTreeShaAsync(client, folder, branch);
        if (treeSha is null) return RepoFileListing.Empty;
        var tree = await client.Git.Tree.GetRecursive(address.Owner, address.Repo, treeSha);
        var files = tree.Tree
            .Where(item => item.Type.TryParse(out var type) && type == TreeType.Blob)
            .Select(item => $"{folder}/{item.Path}")
            .ToList();
        return RepoFileListing.Bounded(files, maxFiles, tree.Truncated);
    }

    // Null when the folder, or the directory that would hold it, is not on the branch.
    private async Task<string?> FolderTreeShaAsync(IGitHubClient client, string folder, string branch)
    {
        var slash = folder.LastIndexOf('/');
        try
        {
            var siblings = slash < 0
                ? await client.Repository.Content.GetAllContentsByRef(address.Owner, address.Repo, branch)
                : await client.Repository.Content.GetAllContentsByRef(address.Owner, address.Repo, folder[..slash], branch);
            return siblings.FirstOrDefault(entry => entry.Path == folder
                && entry.Type.TryParse(out var type) && type == ContentType.Dir)?.Sha;
        }
        catch (NotFoundException ex)
        {
            logger.LogDebug(ex, "{Owner}/{Repo}: {Folder} is not on {Branch}", address.Owner, address.Repo, folder, branch);
            return null;
        }
    }
}
