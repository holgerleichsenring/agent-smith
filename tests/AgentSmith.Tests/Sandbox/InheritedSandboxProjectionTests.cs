using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Configuration.Resolved;
using AgentSmith.Server.Services.Config;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-6968: the COUNTERFACTUAL projection — what a project would inherit if its own
/// sandbox block were empty. The effective projection cannot answer that question, because
/// for a project that has an override it returns the override; these pin the difference.
/// </summary>
public sealed class InheritedSandboxProjectionTests
{
    private static readonly SandboxGlobalConfig Global = new()
    {
        AgentRegistry = "ghcr.io/example",
        AgentVersion = "0.50.0",
        StepTimeoutSeconds = 900,
        RunCommandTimeoutSeconds = 300,
    };

    private static readonly SandboxConfig Overrides = new()
    {
        ToolchainImage = "mirror.example/dotnet/sdk:9.0",
        AgentRegistry = "mirror.example",
        AgentVersion = "0.1.0-canary",
        StepTimeoutSeconds = 1800,
        RunCommandTimeoutSeconds = 600,
    };

    private static (ConfigResolutionPass Pass, InheritedSandboxProjection Projection) Build(
        params (string Name, SandboxConfig? Sandbox)[] projects)
    {
        var options = Options.Create(Global);
        var config = new AgentSmithConfig
        {
            Projects = projects.ToDictionary(
                p => p.Name,
                p => new ResolvedProject { Name = p.Name, Sandbox = p.Sandbox }),
        };
        // 2026-09-22-6c46: the REAL resource resolver, because the projection now reports
        // which of its four layers would answer and a stub could only report a fixed one.
        var resources = WiredResourceResolver.Create();
        var pass = new ConfigResolutionPass(
            options, resources, new StubAgentImageResolver(),
            new StubOrchestratorImageResolver(), config);
        var projection = new InheritedSandboxProjection(
            pass, new AgentVersionResolver(options, new BuildIdentity("abc", "0.60.0")), resources, options,
            config, new SandboxHoldRailDoubles.CountingConfigLoader(config),
            NullLogger<InheritedSandboxProjection>.Instance);
        return (pass, projection);
    }

    [Fact]
    public void InheritedProjection_AProjectThatOverridesAValue_StillReportsWhatItWouldInherit()
    {
        var (pass, projection) = Build(("pinned", Overrides));

        var effective = pass.Resolve(new ResolvedProject { Name = "pinned", Sandbox = Overrides });
        var inherited = projection.ByProject()["pinned"];

        // The effective projection gives the project its own value back…
        effective.StepTimeoutSeconds.Should().Be(
            new ResolvedValue<int>(1800, ResolutionSource.ProjectOverride));
        // …and the counterfactual one says what clearing the control would restore.
        inherited.StepTimeoutSeconds.Should().Be(
            new ResolvedValue<int>(900, ResolutionSource.GlobalDefault));
        inherited.RunCommandTimeoutSeconds.Should().Be(
            new ResolvedValue<int>(300, ResolutionSource.GlobalDefault));
        inherited.AgentRegistry.Value.Should().Be("ghcr.io/example");
        inherited.AgentRegistry.Source.Should().Be(ResolutionSource.GlobalDefault);
        inherited.AgentVersion.Value.Should().Be("0.50.0");
    }

    [Fact]
    public void InheritedProjection_AProjectThatOverridesNothing_ReportsTheSameValueAsTheEffectiveOne()
    {
        var (pass, projection) = Build(("plain", null));

        var effective = pass.Resolve(new ResolvedProject { Name = "plain" });
        var inherited = projection.ByProject()["plain"];

        inherited.StepTimeoutSeconds.Should().Be(effective.StepTimeoutSeconds);
        inherited.RunCommandTimeoutSeconds.Should().Be(effective.RunCommandTimeoutSeconds);
        inherited.ToolchainImage.Should().Be(effective.ToolchainImage);
    }

    [Fact]
    public void InheritedProjection_AToolchainImageWithNoOverride_IsReportedAsRunResolvedNotAsEmpty()
    {
        // The project DOES pin an image; what it would inherit is still the per-run
        // detection, and the form has to say that rather than draw a blank — a blank
        // there reads as "no image", which is the one thing it never means.
        var (_, projection) = Build(("pinned", Overrides));

        var inherited = projection.ByProject()["pinned"];

        inherited.ToolchainImage.Source.Should().Be(ResolutionSource.RunResolved);
        inherited.ToolchainImage.Value.Should().BeNull();
    }

    [Fact]
    public void InheritedProjection_ADraftProjectWithNoRow_FallsBackToTheProcessWideValues()
    {
        var (_, projection) = Build(("plain", null));

        projection.ByProject().Should().NotContainKey("not-created-yet");
        var processWide = projection.ProcessWide();

        processWide.StepTimeoutSeconds.Value.Should().Be(900);
        processWide.RunCommandTimeoutSeconds.Value.Should().Be(300);
        processWide.AgentRegistry.Value.Should().Be("ghcr.io/example");
        processWide.ToolchainImage.Source.Should().Be(ResolutionSource.RunResolved);
    }

    [Fact]
    public void InheritedProjection_NoVersionPinnedAndNoReleaseKnown_ReportsNothingToInheritInsteadOfThrowing()
    {
        var options = Options.Create(new SandboxGlobalConfig { AgentVersion = string.Empty });
        var config = new AgentSmithConfig();
        var resources = WiredResourceResolver.Create();
        var pass = new ConfigResolutionPass(
            options, resources, new StubAgentImageResolver(),
            new StubOrchestratorImageResolver(), config);
        var projection = new InheritedSandboxProjection(
            pass, new AgentVersionResolver(options, new BuildIdentity(null, null)), resources, options,
            config, new SandboxHoldRailDoubles.CountingConfigLoader(config),
            NullLogger<InheritedSandboxProjection>.Instance);

        var act = () => projection.ProcessWide();

        act.Should().NotThrow();
        projection.ProcessWide().AgentVersion.Value.Should().BeNull();
    }

    [Fact]
    public void InheritedSandboxMapper_ProjectsEveryScalarWithItsWireProvenance()
    {
        var (_, projection) = Build(("pinned", Overrides));

        var response = InheritedSandboxMapper.ToResponse(projection);

        response.Projects.Should().ContainKey("pinned");
        response.Projects["pinned"].StepTimeoutSeconds.Should().Be(
            new ConfigResolvedValue<int>(900, "global-default"));
        response.Projects["pinned"].ToolchainImage.Should().Be(
            new ConfigResolvedValue<string>(null, "run-resolved"));
        response.ProcessWide.AgentRegistry.Value.Should().Be("ghcr.io/example");
    }
}
