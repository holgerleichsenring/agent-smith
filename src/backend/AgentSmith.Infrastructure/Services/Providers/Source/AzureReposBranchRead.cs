using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-12-6bfaa: one file, one directory listing and branch existence on a NAMED ref, through
/// the same client factory and token the provider uses. The provider's default-branch reads are
/// these with the resolved default as the ref.
/// </summary>
public sealed class AzureReposBranchRead(
    string project, string repoName, IAzDoClientFactory clientFactory,
    string organizationUrl, string personalAccessToken, ILogger logger)
{
    /// <summary>
    /// The refs filter is a PREFIX match (<c>heads/agent-smith/12</c> also answers
    /// <c>agent-smith/123</c>), so only the exact name counts. Failures throw.
    /// </summary>
    public async Task<bool> ExistsAsync(string branch, CancellationToken ct)
    {
        var refs = await Client().GetRefsAsync(project, repoName, filter: $"heads/{branch}", cancellationToken: ct);
        return refs?.Any(r => string.Equals(r.Name, $"refs/heads/{branch}", StringComparison.Ordinal)) == true;
    }

    public async Task<string?> TryReadFileAsync(string path, string branch, CancellationToken ct)
    {
        try
        {
            using var stream = await Client().GetItemContentAsync(
                project: project, repositoryId: repoName, path: path,
                versionDescriptor: At(branch), cancellationToken: ct);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(ct);
        }
        catch (VssServiceException ex) when (IsNotFound(ex))
        {
            return null;
        }
    }

    // OneLevel returns the directory itself plus its immediate children, each Path carrying a
    // leading "/"; both sides are normalised before the prefix match and only folders are kept.
    public async Task<IReadOnlyList<string>> ListDirectoryAsync(string path, string branch, CancellationToken ct)
    {
        try
        {
            var items = await Client().GetItemsAsync(
                project: project, repositoryId: repoName, scopePath: path,
                recursionLevel: VersionControlRecursionType.OneLevel,
                versionDescriptor: At(branch), cancellationToken: ct);
            logger.LogInformation(
                "ListDirectoryAsync raw: project={Project} repo={Repo} path={Path} branch={Branch} items={Count} sample=[{Sample}]",
                project, repoName, path, branch, items?.Count ?? 0,
                items is null ? "" : string.Join(", ", items.Take(8).Select(i => $"{i.Path}|folder={i.IsFolder}")));
            return items is null ? [] : FolderLeaves(items, path);
        }
        catch (VssServiceException ex) when (IsNotFound(ex))
        {
            return [];
        }
    }

    private static List<string> FolderLeaves(IEnumerable<GitItem> items, string path)
    {
        var normPath = "/" + path.Trim('/');
        var prefix = normPath + "/";
        var result = new List<string>();
        foreach (var item in items)
        {
            if (item.Path is null || item.IsFolder != true) continue;
            var p = item.Path.StartsWith('/') ? item.Path : "/" + item.Path;
            if (string.Equals(p, normPath, StringComparison.Ordinal)) continue;
            var leaf = p.StartsWith(prefix, StringComparison.Ordinal) ? p[prefix.Length..] : p.TrimStart('/');
            if (!string.IsNullOrEmpty(leaf) && !leaf.Contains('/')) result.Add(leaf);
        }
        return result;
    }

    private static GitVersionDescriptor At(string branch) =>
        new() { Version = branch, VersionType = GitVersionType.Branch };

    private static bool IsNotFound(VssServiceException ex) =>
        ex.Message.Contains("could not be found", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase);

    private GitHttpClient Client() => clientFactory.CreateGitClient(organizationUrl, personalAccessToken);
}
