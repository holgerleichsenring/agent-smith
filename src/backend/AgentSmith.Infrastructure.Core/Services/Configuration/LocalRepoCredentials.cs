using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-10-02-5f89a: a local working copy whose auth is empty or <c>none</c> authenticates with
/// nothing, so the legacy-credential migration never fills it and the catalog check never
/// asks for its secret.
/// </summary>
public static class LocalRepoCredentials
{
    private const string NoAuth = "none";

    public static bool IsExempt(RawRepoEntry repo) =>
        repo.Type == RepoType.Local
        && (string.IsNullOrWhiteSpace(repo.Auth) || ConfigNames.AreSame(repo.Auth, NoAuth));
}
