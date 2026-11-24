using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Server.Services.Pocs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.Pocs.Variables;

/// <summary>2026-11-23-933dg: without a master key a pull_secret target is one blocking finding per target, naming the
/// field and the variable; cluster_pulls, a master key or PoCs off name nothing.</summary>
public sealed class PocSealKeyFindingsTests
{
    private readonly StartupFindings _findings = new();

    [Fact]
    public void Report_NoMasterKeyAndAPullSecretTarget_IsOneBlockingFinding()
    {
        Findings(unavailable: "No master key is configured").Report(Config(PocRegistryConfig.PullSecretMode)).Should().Be(1);

        var finding = _findings.All.Should().ContainSingle().Which;
        finding.IsBlocking.Should().BeTrue();
        finding.Field.Should().Be("pocs.targets[main].registry.pull_mode");
        finding.Reason.Should().Contain("pocs.targets[].registry.pull_mode").And.Contain("AGENTSMITH_SECRET_MASTER_KEY");
    }

    [Theory]
    [InlineData(PocRegistryConfig.ClusterPullsMode, "No master key", true)]
    [InlineData(PocRegistryConfig.PullSecretMode, null, true)]
    [InlineData(PocRegistryConfig.PullSecretMode, "No master key", false)]
    public void Report_ClusterPullsAMasterKeyOrPocsOff_NamesNothing(string mode, string? unavailable, bool enabled)
    {
        var config = Config(mode);
        config.Pocs.Enabled = enabled;

        Findings(unavailable).Report(config).Should().Be(0);
        _findings.All.Should().BeEmpty();
    }

    private PocSealKeyFindings Findings(string? unavailable)
    {
        var vault = new Mock<ISecretVault>();
        vault.Setup(v => v.Unavailable).Returns(unavailable);
        var scopes = new ServiceCollection().AddScoped(_ => vault.Object).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new PocSealKeyFindings(_findings, scopes);
    }

    private static AgentSmithConfig Config(string mode)
    {
        var pocs = PocsConfigTests.Sound();
        pocs.Targets[0].Registry!.PullMode = mode;
        return new AgentSmithConfig { Pocs = pocs };
    }
}
