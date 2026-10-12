using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Configuration.Resolved;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Config;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-6c46: the two structured answers the counterfactual projection CAN give. The
/// cpu/memory group has four layers and cannot be told by a value alone, so it names the
/// layer that would answer; the image map is merged per KEY, so its answer is one per key
/// and comes from a table in the code rather than from any configuration. The pod's secrets
/// have no process-wide counterpart and are deliberately absent from the projection.
/// </summary>
public sealed class InheritedSandboxStructuredTests
{
    private static readonly SandboxGlobalConfig Global = new()
    {
        AgentRegistry = "ghcr.io/example",
        AgentVersion = "0.50.0",
    };

    private static InheritedSandboxProjection Build(params (string Name, string Pipeline)[] projects)
    {
        var options = Options.Create(Global);
        var config = new AgentSmithConfig
        {
            Projects = projects.ToDictionary(
                p => p.Name,
                p => new ResolvedProject { Name = p.Name, Pipeline = p.Pipeline }),
        };
        var resources = WiredResourceResolver.Create();
        var pass = new ConfigResolutionPass(
            options, resources, new StubAgentImageResolver(), config);
        return new InheritedSandboxProjection(
            pass, new AgentVersionResolver(options, new BuildIdentity("abc", "0.60.0")), resources, options,
            config, new SandboxHoldRailDoubles.CountingConfigLoader(config),
            NullLogger<InheritedSandboxProjection>.Instance);
    }

    [Fact]
    public void ResourceProjection_AProjectWithNoOverride_NamesTheLayerThatWouldAnswer()
    {
        var projection = Build(("builder", "code"));

        var inherited = projection.ByProject()["builder"].Resources;

        // A code-changing pipeline with no context document reaches the global default —
        // and the control is told WHICH layer that was, not just the numbers, because the
        // numbers alone cannot say it.
        inherited.Layer.Should().Be(SandboxResourceLayer.GlobalDefault);
        inherited.Values.Should().Be(new AgentSmith.Application.Models.SandboxOptions().ToResourceLimits());
    }

    [Fact]
    public void ResourceProjection_ANonCodePipelineField_DoesNotDecideTheAnswer()
    {
        var projection = Build(("scanner", "security-scan"));

        var inherited = projection.ByProject()["scanner"].Resources;

        // 2026-10-12-006d: a ticket's labels or a trigger's default choose a run's pipeline,
        // never this field — so the code-run answer stands beside the light profile a run
        // that changes no code is held to, whatever the field says.
        inherited.Layer.Should().Be(SandboxResourceLayer.GlobalDefault);
        inherited.Values.Should().Be(new AgentSmith.Application.Models.SandboxOptions().ToResourceLimits());
        inherited.NoCodeValues.Should().Be(ResourceLimits.LightProfile);
    }

    [Fact]
    public void ResourceProjection_AProjectWithNoPipelineField_ReportsWhatACodeRunGets()
    {
        // The observed defect: an empty field read as "changes no code", so the tab claimed
        // the light profile for a project whose ticket runs are all `code`.
        var projection = Build(("ticketed", ""));

        var inherited = projection.ByProject()["ticketed"].Resources;

        inherited.Layer.Should().Be(SandboxResourceLayer.GlobalDefault);
        inherited.Values.Should().NotBe(ResourceLimits.LightProfile);
        projection.ProcessWide().Resources.Layer.Should().Be(SandboxResourceLayer.GlobalDefault);
    }

    [Fact]
    public void ImageProjection_ACodeDefaultTable_IsProjectedWithPerKeyProvenance()
    {
        var projection = Build(("builder", "code"));

        var images = projection.ByProject()["builder"].Images;

        images.Should().HaveCount(ToolchainImageCatalog.KnownLanguages.Count);
        images["dotnet"].Value.Should().Be(ToolchainImageCatalog.KnownLanguages["dotnet"]);
        // Per KEY, and from the CODE — not from a configuration setting an operator could
        // go looking for. Every key is an answer, including ones no project names.
        images["node"].Source.Should().Be(ResolutionSource.CodeDefault);
        images.Should().ContainKey("python");
    }

    [Fact]
    public void ImageProjection_OnTheWire_CarriesEveryKeyAndItsSourceName()
    {
        var response = InheritedSandboxMapper.ToResponse(Build(("builder", "code")));

        var images = response.Projects["builder"].Images;

        images["dotnet"].Source.Should().Be("code-default");
        images["dotnet"].Value.Should().Be(ToolchainImageCatalog.KnownLanguages["dotnet"]);
    }

    [Fact]
    public void ResourceProjection_OnTheWire_CarriesTheLayerAndTheNoCodeValues()
    {
        var response = InheritedSandboxMapper.ToResponse(Build(("scanner", "security-scan")));

        var resources = response.Projects["scanner"].Resources;

        resources.Layer.Should().Be("global-default");
        resources.Values.CpuLimit.Should().Be(new AgentSmith.Application.Models.SandboxOptions().CpuLimit);
        resources.NoCodeValues.CpuLimit.Should().Be(ResourceLimits.LightProfile.CpuLimit);
        resources.NoCodeValues.MemoryLimit.Should().Be(ResourceLimits.LightProfile.MemoryLimit);
    }

    /// <summary>The global sandbox block has no secrets field, so there is nothing for the
    /// projection to carry — and a blank one would read as an inherited empty set.</summary>
    [Fact]
    public void InheritedProjection_CarriesNoSecrets_BecauseNothingProcessWideHoldsAny()
    {
        typeof(InheritedSandboxSettings).GetProperty("Secrets").Should().BeNull();
        typeof(ConfigInheritedSandbox).GetProperty("Secrets").Should().BeNull();
    }
}
