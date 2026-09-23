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
/// 2026-09-23-2446: the hold window is the ONE field of the inherited projection read live.
/// Every other value in the project form's sandbox tab reaches its consumer at run start or
/// after a restart, so a placeholder frozen at composition matches a frozen effect. This one
/// is resolved through the configuration loader by a reaper on every scan, so a frozen
/// placeholder would disagree with the window in force from the moment an operator edits
/// anything — the only moment the control matters.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class InheritedSandboxHoldWindowTests : IDisposable
{
    private readonly string? _before =
        Environment.GetEnvironmentVariable(SandboxHoldWindow.EnvironmentVariable);

    public InheritedSandboxHoldWindowTests() =>
        Environment.SetEnvironmentVariable(SandboxHoldWindow.EnvironmentVariable, null);

    public void Dispose() =>
        Environment.SetEnvironmentVariable(SandboxHoldWindow.EnvironmentVariable, _before);

    [Fact]
    public void InheritedProjection_AProjectThatSetsItsOwnHoldWindow_StillReportsWhatItWouldInherit()
    {
        var (projection, _) = Build(processWide: 60, projectOverride: 600);

        // The counterfactual: the project's own 600 is exactly what is being emptied, so what
        // the control has to name is the 60 that clearing it would restore.
        projection.ByProject()["proj"].HoldSeconds
            .Should().Be(new ResolvedValue<int>(60, ResolutionSource.GlobalDefault));
    }

    [Fact]
    public void InheritedProjection_AProcessWideValueEditedAfterStartup_IsReportedWithoutARestart()
    {
        var (projection, loader) = Build(processWide: 60, projectOverride: null);
        projection.ProcessWide().HoldSeconds.Value.Should().Be(60);

        // An operator edits the process-wide window in the settings form. The options
        // instance this projection was composed with still holds 60 and always will; the
        // reaper's next scan will use 900, and so must the placeholder.
        loader.Config = Catalog(processWide: 900, projectOverride: null);

        projection.ProcessWide().HoldSeconds.Value.Should().Be(900);
        projection.ByProject()["proj"].HoldSeconds.Value.Should().Be(900);
    }

    [Fact]
    public void InheritedProjection_AHoldWindowFromTheEnvironmentVariable_NamesThatLeg()
    {
        Environment.SetEnvironmentVariable(SandboxHoldWindow.EnvironmentVariable, "240");
        var (projection, _) = Build(processWide: null, projectOverride: null);

        // An installation whose catalog says nothing still has a window. Calling that the
        // global default would send the operator to a settings form that holds no value.
        projection.ProcessWide().HoldSeconds
            .Should().Be(new ResolvedValue<int>(240, ResolutionSource.EnvironmentVariable));
        InheritedSandboxMapper.ToResponse(projection).ProcessWide.HoldSeconds
            .Should().Be(new ConfigResolvedValue<int>(240, "environment-variable"));
    }

    [Fact]
    public void InheritedProjection_AnUnconfiguredHoldWindow_ReportsThreeMinutesAsTheBuiltInDefault()
    {
        var (projection, _) = Build(processWide: null, projectOverride: null);

        projection.ProcessWide().HoldSeconds
            .Should().Be(new ResolvedValue<int>(180, ResolutionSource.CodeDefault));
    }

    [Fact]
    public void InheritedProjection_TheHoldWindow_IsReadOncePerProjection()
    {
        var (projection, loader) = Build(processWide: 60, projectOverride: null);

        projection.ByProject();

        // The loader re-assembles the whole catalog from the document store on every call,
        // and every row's counterfactual answer is the same process-wide one.
        loader.Reads.Should().Be(1);
    }

    private static AgentSmithConfig Catalog(int? processWide, int? projectOverride) => new()
    {
        Sandbox = new SandboxGlobalConfig { HoldSeconds = processWide },
        Projects = new Dictionary<string, ResolvedProject>(StringComparer.Ordinal)
        {
            ["proj"] = new()
            {
                Name = "proj",
                Sandbox = projectOverride is null ? null : new SandboxConfig { HoldSeconds = projectOverride },
            },
        },
    };

    private static (InheritedSandboxProjection Projection, SandboxHoldRailDoubles.CountingConfigLoader Loader)
        Build(int? processWide, int? projectOverride)
    {
        var config = Catalog(processWide, projectOverride);
        // The options instance is the COMPOSITION-time one and is deliberately never
        // refreshed — which is the whole reason this field cannot be read from it.
        var options = Options.Create(config.Sandbox);
        var resources = WiredResourceResolver.Create();
        var pass = new ConfigResolutionPass(
            options, resources, new StubAgentImageResolver(), new StubOrchestratorImageResolver(), config);
        var loader = new SandboxHoldRailDoubles.CountingConfigLoader(config);
        return (new InheritedSandboxProjection(
            pass, new AgentVersionResolver(options, new BuildIdentity("abc", "0.60.0")), resources, options,
            config, loader, NullLogger<InheritedSandboxProjection>.Instance), loader);
    }
}
