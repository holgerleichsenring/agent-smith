using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-02-5f89g: a git credential for tests whose subject is not the credential — every
/// remote repo answers with one fixed token, a local repo with none, the way the real resolver
/// treats them.
/// </summary>
public static class TestGitCredentials
{
    public const string Token = "test-git-token";

    public static IGitTokenResolver Resolver { get; } = new FixedTokenResolver();

    private sealed class FixedTokenResolver : IGitTokenResolver
    {
        public GitCredential For(RepoConnection repo) =>
            repo.Type == RepoType.Local ? GitCredential.None : new GitCredential(Token);
    }
}
