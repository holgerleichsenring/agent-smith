using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-23-c81ea: every file under one folder of a branch — the items call scoped to the folder with FULL recursion,
/// folders dropped, each path repo-relative without Azure's leading slash.
/// </summary>
public sealed class AzureReposFolderFiles(
    string project, string repoName, IAzDoClientFactory clientFactory,
    string organizationUrl, string personalAccessToken, ILogger logger)
{
    public async Task<RepoFileListing> ListAsync(string path, string branch, int maxFiles, CancellationToken ct)
    {
        var folder = path.Replace('\\', '/').Trim('/');
        try
        {
            var items = await clientFactory.CreateGitClient(organizationUrl, personalAccessToken).GetItemsAsync(
                project: project, repositoryId: repoName, scopePath: folder,
                recursionLevel: VersionControlRecursionType.Full,
                versionDescriptor: new GitVersionDescriptor { Version = branch, VersionType = GitVersionType.Branch },
                cancellationToken: ct);
            var files = (items ?? [])
                .Where(item => !item.IsFolder && item.Path is not null)
                .Select(item => item.Path.TrimStart('/'))
                .Where(file => file.StartsWith(folder + "/", StringComparison.Ordinal))
                .ToList();
            return RepoFileListing.Bounded(files, maxFiles, hostTruncated: false);
        }
        catch (VssServiceException ex) when (AzureReposBranchRead.IsNotFound(ex))
        {
            logger.LogDebug(ex, "{Project}/{Repo}: {Folder} is not on {Branch}", project, repoName, folder, branch);
            return RepoFileListing.Empty;
        }
    }
}
