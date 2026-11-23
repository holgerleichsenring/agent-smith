using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Registry;

/// <summary>
/// 2026-11-23-933de: a target's registry block states its own rule — ACR login-server shape, a registry path, pull user
/// and pull token required (with cluster_pulls too), a known pull mode — and every target needs one.
/// </summary>
public sealed class PocRegistryConfigTests
{
    [Theory]
    [InlineData("registry.example.com")]
    [InlineData("abc.azurecr.io")]
    [InlineData("Poc.azurecr.io")]
    [InlineData("pocregistry.azurecr.io.evil.example")]
    public void PocRegistryConfig_BadLoginServer_Refused(string loginServer)
    {
        var registry = PocsConfigTests.SoundRegistry();
        registry.LoginServer = loginServer;

        registry.Problems().Should().ContainSingle().Which.Should()
            .StartWith($"pocs.targets[].registry.login_server is '{loginServer}'");
    }

    [Theory]
    [InlineData("pocregistry.azurecr.io")]
    [InlineData("pocregistry-e4cqa8ewdcfyabhc.azurecr.io")]
    [InlineData("pocregistry.azurecr.us")]
    [InlineData("pocregistry.azurecr.cn")]
    public void PocRegistryConfig_HashLabelAndSovereignSuffix_Accepted(string loginServer)
    {
        var registry = PocsConfigTests.SoundRegistry();
        registry.LoginServer = loginServer;

        registry.Problems().Should().BeEmpty();
    }

    [Fact]
    public void PocRegistryConfig_EmptyPullUser_Refused()
    {
        var registry = PocsConfigTests.SoundRegistry();
        registry.PullUser = " ";

        registry.Problems().Should().ContainSingle().Which.Should().StartWith("pocs.targets[].registry.pull_user is empty");
    }

    [Fact]
    public void PocRegistryConfig_EmptyPullAuthWithClusterPulls_Refused()
    {
        var registry = PocsConfigTests.SoundRegistry();
        registry.PullMode = PocRegistryConfig.ClusterPullsMode;
        registry.PullAuth = "";

        registry.Problems().Should().ContainSingle().Which.Should().StartWith("pocs.targets[].registry.pull_auth is empty");
    }

    [Theory]
    [InlineData("type", "ecr", "pocs.targets[].registry.type is 'ecr'")]
    [InlineData("repository", "Pocs/../x", "pocs.targets[].registry.repository is 'Pocs/../x'")]
    [InlineData("mode", "kubelet", "pocs.targets[].registry.pull_mode is 'kubelet'")]
    public void PocRegistryConfig_UnknownTypeBadRepositoryOrMode_RefusedNamingKey(string field, string value, string expected)
    {
        var registry = PocsConfigTests.SoundRegistry();
        if (field == "type") registry.Type = value;
        else if (field == "repository") registry.Repository = value;
        else registry.PullMode = value;

        registry.Problems().Should().ContainSingle().Which.Should().StartWith(expected);
    }

    [Fact]
    public void PocsConfig_TargetWithoutRegistry_RefusedNamingKey()
    {
        var pocs = PocsConfigTests.Sound();
        pocs.Targets[0].Registry = null;

        pocs.Problems().Should().ContainSingle().Which.Should().StartWith("pocs.targets[].registry of target 'main' is missing");
    }

    [Fact]
    public void PocsConfig_ABadRegistryBlock_IsRefusedNamingTheTarget()
    {
        var pocs = PocsConfigTests.Sound();
        pocs.Targets[0].Registry!.PullUser = "";

        pocs.Problems().Should().ContainSingle().Which.Should().EndWith("(target 'main')");
    }

    [Fact]
    public void SettingsBlockRule_ImportOfABadRegistryBlock_IsRefused()
    {
        var pocs = PocsConfigTests.Sound();
        pocs.Targets[0].Registry!.LoginServer = "registry.example.com";

        var import = () => SettingsBlockRule.Validate(new RawAgentSmithConfig { Pocs = pocs });

        import.Should().Throw<ConfigurationException>().WithMessage("*pocs.targets[].registry.login_server*");
    }

    [Fact]
    public void Load_ARegistryBlock_BindsFromYaml()
    {
        var file = Path.Combine(Path.GetTempPath(), $"agentsmith-registry-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(file, "projects: {}\npocs:\n  targets:\n    - name: main\n      source_repo: gh/poc-src\n"
                + "      registry:\n        login_server: pocregistry.azurecr.io\n        pull_user: poc-pull\n"
                + "        pull_auth: poc_pull_token\n        pull_mode: cluster_pulls\n");
            var registry = PocsConfigTests.LoadFile(file).Pocs.Targets.Single().Registry!;

            (registry.Type, registry.LoginServer, registry.Repository, registry.PullMode)
                .Should().Be(("acr", "pocregistry.azurecr.io", "pocs", "cluster_pulls"));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
