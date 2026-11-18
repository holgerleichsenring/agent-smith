using AgentSmith.Application.Services.Admin;
using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using FluentAssertions;

namespace AgentSmith.Tests.Admin;

/// <summary>2026-11-17-f3a8a: the project a run on the installation executes against.</summary>
public sealed class InstallationProjectTests
{
    [Fact]
    public void Build_AdminTurn_RunsOnTheAdminAgent()
    {
        var config = Config("ops");

        var project = InstallationProject.Build(config, PipelinePresets.AdminTurnName);

        project.Name.Should().Be(InstallationScope.Name);
        project.Agent.Should().BeSameAs(config.Agents["ops"]);
        project.Repos.Should().ContainSingle();
    }

    [Fact]
    public void Build_AMasterlessInstallationRun_NeedsNoAdminAgent()
    {
        var project = InstallationProject.Build(Config(null), "config");

        project.Agent.Should().NotBeNull();
    }

    [Theory]
    [InlineData(null, "names none")]
    [InlineData("ghost", "'ghost', which is not configured")]
    public void AdminAgent_UnsetOrUnknown_IsRefusedNamingTheKey(string? adminAgent, string reason)
    {
        var act = () => InstallationProject.AdminAgent(Config(adminAgent));

        act.Should().Throw<ConfigurationException>()
            .Which.Message.Should().Contain("dialogue.admin_agent").And.Contain(reason).And.Contain("ops");
    }

    [Fact]
    public void ProjectNameValidator_AProjectNamedLikeTheInstallation_IsBlocking()
    {
        var config = new AgentSmithConfig { Projects = { [InstallationScope.Name] = new ResolvedProject { Name = InstallationScope.Name } } };

        ProjectNameConfigValidator.Findings(config).Should().ContainSingle()
            .Which.Severity.Should().Be(StartupFindingSeverity.Blocking);
    }

    [Fact]
    public void Presets_AdminTurn_IsAKnownUnroutableDesignTurnWithItsMaster()
    {
        PipelinePresets.TryResolve(PipelinePresets.AdminTurnName).Should().NotBeNull();
        PipelinePresets.Routable.Should().NotContain(PipelinePresets.AdminTurnName);
        PipelinePresets.MasterFor(PipelinePresets.AdminTurnName).Should().Be(PipelinePresets.AdminAssistantMaster);
        PipelinePresets.RunKindOf(PipelinePresets.AdminTurnName).Should().Be(AgentSmith.Contracts.Services.RegisteredRunKind.DesignTurn);
    }

    private static AgentSmithConfig Config(string? adminAgent) => new()
    {
        Agents = { ["ops"] = new AgentConfig() },
        Dialogue = new DialogueGlobalConfig { AdminAgent = adminAgent },
    };
}
