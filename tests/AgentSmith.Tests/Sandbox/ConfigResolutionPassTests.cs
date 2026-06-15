using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Configuration.Resolved;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0270a: characterization of the single resolution pass. These pin the
/// effective values + provenance that the run path and the dashboard both read —
/// the same arithmetic the deleted SandboxGlobalConfig.Resolve* /
/// PipelineCostCapConfig.ResolveFor used, now in one place.
/// </summary>
public sealed class ConfigResolutionPassTests
{
    private static ConfigResolutionPass MakePass(
        SandboxGlobalConfig? global = null,
        AgentSmithConfig? config = null,
        IAgentImageResolver? agentImage = null) =>
        new(Options.Create(global ?? new SandboxGlobalConfig()),
            new StubSandboxResourceResolver(),
            agentImage ?? new StubAgentImageResolver(),
            new StubOrchestratorImageResolver(),
            config ?? new AgentSmithConfig());

    [Fact]
    public void ConfigResolutionPass_NoOverride_UsesGlobalDefaultWithSourceGlobal()
    {
        var pass = MakePass(new SandboxGlobalConfig { StepTimeoutSeconds = 1200, RunCommandTimeoutSeconds = 420 });

        var settings = pass.Resolve(new ResolvedProject { Name = "p" });

        settings.StepTimeoutSeconds.Should().Be(new ResolvedValue<int>(1200, ResolutionSource.GlobalDefault));
        settings.RunCommandTimeoutSeconds.Should().Be(new ResolvedValue<int>(420, ResolutionSource.GlobalDefault));
        settings.SandboxResources.Source.Should().Be(ResolutionSource.GlobalDefault);
    }

    [Fact]
    public void ConfigResolutionPass_ProjectOverride_WinsOverGlobalWithSourceOverride()
    {
        var pass = MakePass(new SandboxGlobalConfig { StepTimeoutSeconds = 900, RunCommandTimeoutSeconds = 300 });
        var project = new ResolvedProject
        {
            Name = "p",
            Sandbox = new SandboxConfig { StepTimeoutSeconds = 1800, RunCommandTimeoutSeconds = 600 },
        };

        var settings = pass.Resolve(project);

        settings.StepTimeoutSeconds.Should().Be(new ResolvedValue<int>(1800, ResolutionSource.ProjectOverride));
        settings.RunCommandTimeoutSeconds.Should().Be(new ResolvedValue<int>(600, ResolutionSource.ProjectOverride));
    }

    [Fact]
    public void ConfigResolutionPass_ToolchainImage_MarkedResolvedPerRun_NotFabricated()
    {
        var settings = MakePass().Resolve(new ResolvedProject { Name = "p" });

        settings.ToolchainImage.Source.Should().Be(ResolutionSource.RunResolved);
        settings.ToolchainImage.Value.Should().BeNull();
    }

    [Fact]
    public void ConfigResolutionPass_ToolchainOverride_IsProjectOverrideWithValue()
    {
        var project = new ResolvedProject
        {
            Name = "p",
            Sandbox = new SandboxConfig { ToolchainImage = "mcr.microsoft.com/dotnet/sdk:8.0" },
        };

        var settings = MakePass().Resolve(project);

        settings.ToolchainImage.Should().Be(
            new ResolvedValue<string>("mcr.microsoft.com/dotnet/sdk:8.0", ResolutionSource.ProjectOverride));
    }

    [Fact]
    public void ResolveCostCap_KnownPipeline_OverrideElseGlobalDefault()
    {
        var over = new CostCapValues { Usd = 10m, Tokens = 1_000_000 };
        var config = new AgentSmithConfig
        {
            PipelineCostCap = new PipelineCostCapConfig
            {
                Default = new CostCapValues { Usd = 5m, Tokens = 500_000 },
                PerPipeline = new Dictionary<string, CostCapValues>(StringComparer.OrdinalIgnoreCase)
                { ["api-security-scan"] = over },
            },
        };
        var pass = MakePass(config: config);

        var resolved = pass.ResolveCostCap("api-security-scan");
        resolved.Value.Should().BeSameAs(over);
        resolved.Source.Should().Be(ResolutionSource.ProjectOverride);

        var fallback = pass.ResolveCostCap("fix-bug");
        fallback.Value.Should().BeSameAs(config.PipelineCostCap.Default);
        fallback.Source.Should().Be(ResolutionSource.GlobalDefault);
    }

    [Fact]
    public void Materialize_ImageVersionMissing_CapturesErrorInsteadOfThrowing()
    {
        // Real AgentImageResolver throws on an empty agent version — Materialize
        // must capture that per project, never crash the server / dashboard.
        var throwing = new AgentImageResolver(Options.Create(new SandboxGlobalConfig()));
        var config = new AgentSmithConfig
        {
            Projects = new() { ["p"] = new ResolvedProject { Name = "p" } },
        };
        var pass = MakePass(config: config, agentImage: throwing);

        var act = () => pass.Materialize();

        act.Should().NotThrow();
        pass.Materialize().Projects["p"].ResolutionError.Should().NotBeNullOrEmpty();
        // Non-image fields still resolved despite the error.
        pass.Materialize().Projects["p"].StepTimeoutSeconds.Value.Should().Be(900);
    }
}
