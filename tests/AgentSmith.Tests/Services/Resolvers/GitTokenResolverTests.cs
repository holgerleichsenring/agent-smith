using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Resolvers;

/// <summary>
/// Shared GitTokenResolver replaces the duplicated switch across
/// HostSourceCloner and CheckoutSourceHandler. Tests run with each env var
/// explicitly set/cleared so they don't depend on the host's dev environment.
/// </summary>
[Collection("EnvVars")]
public sealed class GitTokenResolverTests : IDisposable
{
    private readonly Dictionary<string, string?> _saved = new();

    public GitTokenResolverTests()
    {
        foreach (var name in new[] { "GITHUB_TOKEN", "GITLAB_TOKEN", "AZURE_DEVOPS_TOKEN" })
        {
            _saved[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    public void Dispose()
    {
        foreach (var (name, value) in _saved)
            Environment.SetEnvironmentVariable(name, value);
    }

    [Theory]
    [InlineData(RepoType.GitHub, "GITHUB_TOKEN")]
    [InlineData(RepoType.GitLab, "GITLAB_TOKEN")]
    [InlineData(RepoType.AzureDevOps, "AZURE_DEVOPS_TOKEN")]
    public void Resolve_KnownType_ReadsMatchingEnvVar(RepoType type, string envVar)
    {
        Environment.SetEnvironmentVariable(envVar, "test-pat-12345");
        try
        {
            GitTokenResolver.Resolve(type).Should().Be("test-pat-12345");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, null);
        }
    }

    [Fact]
    public void Resolve_LocalType_ReturnsNull() =>
        GitTokenResolver.Resolve(RepoType.Local).Should().BeNull();

    [Fact]
    public void Resolve_KnownTypeButEnvVarUnset_ReturnsNull() =>
        GitTokenResolver.Resolve(RepoType.AzureDevOps).Should().BeNull();
}
