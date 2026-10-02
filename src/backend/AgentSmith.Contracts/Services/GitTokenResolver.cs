using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-02-5f89g: <see cref="IGitTokenResolver"/> over <see cref="ICredentialResolver"/>. It
/// used to map the repo TYPE to GITHUB_TOKEN, GITLAB_TOKEN or AZURE_DEVOPS_TOKEN, which put one
/// token behind every repo of a type; the repo's own auth secret answers now.
/// </summary>
public sealed class GitTokenResolver(ICredentialResolver credentials) : IGitTokenResolver
{
    public GitCredential For(RepoConnection repo) =>
        repo.Type == RepoType.Local ? GitCredential.None : new GitCredential(credentials.For(repo));
}
