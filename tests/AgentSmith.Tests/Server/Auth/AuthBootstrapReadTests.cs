using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503b: where the auth block comes from. A Kubernetes operator sets the authority of a
/// cluster's identity provider as an environment variable — a mounted ConfigMap is shared
/// and the authority is not — so the environment wins over the file, per field.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class AuthBootstrapReadTests : IDisposable
{
    private readonly string _configPath =
        Path.Combine(Path.GetTempPath(), $"agentsmith-auth-read-{Guid.NewGuid():N}.yml");

    private readonly string? _authorityBefore =
        Environment.GetEnvironmentVariable(AuthEnvironmentOverlay.AuthorityVar);

    [Fact]
    public void Bootstrap_AuthorityFromEnvironment_OverridesTheFile()
    {
        File.WriteAllText(_configPath, """
            auth:
              authority: https://the-file-said-this
              audience: from-the-file
              enforce: true
            """);
        Environment.SetEnvironmentVariable(
            AuthEnvironmentOverlay.AuthorityVar, "https://the-environment-said-this");

        var auth = Read().Auth;

        auth!.Authority.Should().Be("https://the-environment-said-this");
        auth.Audience.Should().Be("from-the-file", "only the field that was set is overridden");
        auth.Enforce.Should().BeTrue();
    }

    [Fact]
    public void Bootstrap_AuthorityFromEnvironmentAlone_NeedsNoAuthBlockInTheFile()
    {
        File.WriteAllText(_configPath, "persistence:\n  provider: sqlite\n");
        Environment.SetEnvironmentVariable(
            AuthEnvironmentOverlay.AuthorityVar, "https://only-the-environment-said-this");

        Read().Auth!.Authority.Should().Be("https://only-the-environment-said-this");
    }

    [Fact]
    public void Bootstrap_NoAuthBlockAndNoEnvironment_IsNoBlockAtAll()
    {
        File.WriteAllText(_configPath, "persistence:\n  provider: sqlite\n");

        Read().Auth.Should().BeNull("absent and unusable are different states");
    }

    private BootstrapConfig Read() => new BootstrapConfigReader(
        new FixedConfigPath(_configPath), new RawConfigYaml(), new AuthEnvironmentOverlay()).Read();

    private sealed record FixedConfigPath(string ConfigPath) : IConfigStoreLocation;

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(AuthEnvironmentOverlay.AuthorityVar, _authorityBefore);
        if (File.Exists(_configPath)) File.Delete(_configPath);
    }
}
