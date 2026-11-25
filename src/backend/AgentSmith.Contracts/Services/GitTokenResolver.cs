using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-02-5f89g: <see cref="IGitTokenResolver"/> over <see cref="ICredentialResolver"/>. It
/// used to map the repo TYPE to GITHUB_TOKEN, GITLAB_TOKEN or AZURE_DEVOPS_TOKEN, which put one
/// token behind every repo of a type; the repo's own auth secret answers now.
/// 2026-11-25-d0e2: an Azure Repos token that is an Entra access token goes as a bearer header (<see cref="AzureDevOpsBearer"/>).
/// </summary>
public sealed class GitTokenResolver(ICredentialResolver credentials) : IGitTokenResolver
{
    public GitCredential For(RepoConnection repo)
    {
        if (repo.Type == RepoType.Local) return GitCredential.None;
        var token = credentials.For(repo);
        var bearer = repo.Type == RepoType.AzureDevOps && AzureDevOpsBearer.Is(token);
        return new GitCredential(token, bearer, bearer ? Origin(repo.Url) : null);
    }

    // The bearer header goes to the remote's own host only: scheme and authority, as git matches http.<url>.* by prefix.
    private static string? Origin(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? $"{uri.Scheme}://{uri.Authority}/" : null;
}
