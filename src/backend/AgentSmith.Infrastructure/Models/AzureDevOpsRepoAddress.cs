using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Models;

/// <summary>
/// 2026-10-26-cb25a: an Azure Repos repository as every Azure DevOps caller addresses it — the organization (or
/// Server collection) URL, project, repository and web URL. It reads the three shapes one parser has to know:
/// dev.azure.com/{org}/{project}/_git/{repo}, {org}.visualstudio.com/[{collection}/]{project}/_git/{repo} and a
/// Server's host[:port]/[{virtual dir}/]{collection}/{project}/_git/{repo}. The organization URL is everything
/// before /{project}/_git/, port included; _git matches in any case; only a TRAILING ".git" is dropped. Project
/// and repository keep the escaped form the URL writes, so every URL built from them keeps one spelling.
/// </summary>
public sealed record AzureDevOpsRepoAddress(string OrganizationUrl, string Project, string Repo, string RepoUrl)
{
    private const string GitSegment = "_git";

    public static AzureDevOpsRepoAddress Parse(string url) =>
        TryParse(url, out var address) ? address! : throw new ConfigurationException($"Invalid Azure Repos URL: {url}");

    public static bool TryParse(string? url, out AzureDevOpsRepoAddress? address)
    {
        address = null;
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(RepoKey.WithoutGitSuffix(url.Trim()), UriKind.Absolute, out var uri)) return false;
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        var git = Array.FindIndex(segments, s => s.Equals(GitSegment, StringComparison.OrdinalIgnoreCase));
        if (git < 1 || git + 1 >= segments.Length || !NamesAProject(uri, git)) return false;
        var organizationUrl = $"{uri.Scheme}://{uri.Authority}{string.Concat(segments[..(git - 1)].Select(s => "/" + s))}";
        var (project, repo) = (segments[git - 1], segments[git + 1]);
        address = new AzureDevOpsRepoAddress(organizationUrl, project, repo, $"{organizationUrl}/{project}/{GitSegment}/{repo}");
        return true;
    }

    // On the cloud host the organization is the first segment, so dev.azure.com/{org}/_git/{repo} names no project.
    private static bool NamesAProject(Uri uri, int git) =>
        git >= 2 || !uri.Host.Equals(AzureDevOpsOrganizationUrl.CloudHost, StringComparison.OrdinalIgnoreCase);
}
