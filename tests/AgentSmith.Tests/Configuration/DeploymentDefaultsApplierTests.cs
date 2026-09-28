using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;
using Xunit;

namespace AgentSmith.Tests.Configuration;

public class DeploymentDefaultsApplierTests
{
    private readonly DeploymentDefaultsApplier _applier = new();

    [Fact]
    public void Apply_DeploymentVersion_FeedsTheSandboxAgent()
    {
        var raw = new RawAgentSmithConfig { Deployment = new DeploymentConfig { Registry = "reg", Version = "1.2.3" } };

        _applier.Apply(raw);

        raw.Sandbox.AgentRegistry.Should().Be("reg");
        raw.Sandbox.AgentVersion.Should().Be("1.2.3");
    }

    [Fact]
    public void Apply_LegacySandboxVersionSet_WinsOverDeployment()
    {
        var raw = new RawAgentSmithConfig
        {
            Deployment = new DeploymentConfig { Version = "1.2.3" },
            Sandbox = new SandboxGlobalConfig { AgentVersion = "0.48.0" },
        };

        _applier.Apply(raw);

        raw.Sandbox.AgentVersion.Should().Be("0.48.0");
    }

    [Fact]
    public void Apply_SandboxRegistryStillDefault_DeploymentTakesAuthority()
    {
        var raw = new RawAgentSmithConfig { Deployment = new DeploymentConfig { Registry = "reg", Version = "1.2.3" } };
        raw.Sandbox.AgentRegistry.Should().Be(AgentImageDefaults.DefaultRegistry); // baseline

        _applier.Apply(raw);

        raw.Sandbox.AgentRegistry.Should().Be("reg");
    }

    [Fact]
    public void Apply_NoDeployment_LeavesEverythingUntouched()
    {
        var raw = new RawAgentSmithConfig();

        _applier.Apply(raw);

        raw.Sandbox.AgentVersion.Should().BeEmpty();
        raw.Sandbox.AgentRegistry.Should().Be(AgentImageDefaults.DefaultRegistry);
    }
}
