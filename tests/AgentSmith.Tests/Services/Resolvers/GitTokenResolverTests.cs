using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Resolvers;

/// <summary>
/// p0125c-followup: shared GitTokenResolver replaces the duplicated switch
/// across HostSourceCloner and CheckoutSourceHandler. Tests run with each
/// env var explicitly set/cleared so they don't depend on the host's
/// AGENT_SMITH dev environment.
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
    [InlineData("GitHub", "GITHUB_TOKEN")]
    [InlineData("github", "GITHUB_TOKEN")]
    [InlineData("GitLab", "GITLAB_TOKEN")]
    [InlineData("gitlab", "GITLAB_TOKEN")]
    [InlineData("AzureRepos", "AZURE_DEVOPS_TOKEN")]
    [InlineData("azurerepos", "AZURE_DEVOPS_TOKEN")]
    public void Resolve_KnownType_ReadsMatchingEnvVar(string sourceType, string envVar)
    {
        Environment.SetEnvironmentVariable(envVar, "test-pat-12345");
        try
        {
            GitTokenResolver.Resolve(sourceType).Should().Be("test-pat-12345");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, null);
        }
    }

    [Theory]
    [InlineData("Local")]
    [InlineData("Bitbucket")]
    [InlineData("")]
    public void Resolve_UnknownType_ReturnsNull(string sourceType) =>
        GitTokenResolver.Resolve(sourceType).Should().BeNull();

    [Fact]
    public void Resolve_KnownTypeButEnvVarUnset_ReturnsNull() =>
        GitTokenResolver.Resolve("AzureRepos").Should().BeNull();
}
