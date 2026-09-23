using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Configuration.Resolved;
using AgentSmith.Server.Services.Config;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-23-2446: the process-wide hold-window fallback has ONE home, because two callers
/// need the same answer — the scan-time resolver that decides whether a labelled sandbox is
/// a corpse, and the project form's inherited-value projection that has to name the window
/// the next scan will use. One algorithm over two inputs would still be two answers.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class SandboxHoldWindowChainTests : IDisposable
{
    private const string Project = "project-a";

    private readonly string? _before =
        Environment.GetEnvironmentVariable(SandboxHoldWindow.EnvironmentVariable);

    public void Dispose() =>
        Environment.SetEnvironmentVariable(SandboxHoldWindow.EnvironmentVariable, _before);

    [Fact]
    public void HoldWindow_TheScanTimeResolver_StillResolvesProjectThenProcessWideThenEnvironmentThenDefault()
    {
        Environment.SetEnvironmentVariable(SandboxHoldWindow.EnvironmentVariable, "45");

        // The project's own value wins over the process-wide field…
        Resolve(processWide: 60, projectOverride: 600).For(Project).Should().Be(TimeSpan.FromSeconds(600));
        // …the process-wide field wins over the environment…
        Resolve(processWide: 60, projectOverride: null).ProcessWide.Should().Be(TimeSpan.FromSeconds(60));
        // …the environment wins over the built-in…
        Resolve(processWide: null, projectOverride: null).ProcessWide.Should().Be(TimeSpan.FromSeconds(45));

        Environment.SetEnvironmentVariable(SandboxHoldWindow.EnvironmentVariable, null);
        // …and with nothing set at all it is three minutes, exactly as before the move.
        Resolve(processWide: null, projectOverride: null).ProcessWide.Should().Be(TimeSpan.FromMinutes(3));
    }

    [Fact]
    public void HoldWindow_TheEnvironmentVariableName_AppearsInExactlyOnePlaceInTheBackend()
    {
        var sites = Architecture.ArchitectureSources.HandWrittenBackendFiles()
            .Where(f => File.ReadAllText(f).Contains("SANDBOX_HOLD_SECONDS", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        // A name spelled twice is a name that can be changed once. The doc comment on the
        // configuration field cites the constant instead of repeating the string.
        sites.Should().Equal(["SandboxHoldWindow.cs"]);
    }

    [Fact]
    public void HoldWindow_ANegativeValue_IsClampedToZeroByTheSharedChain()
    {
        // A negative window is not a shorter hold, it is nonsense — and zero already means
        // hold nothing, so that is where it lands, on both legs of the chain.
        SandboxHoldWindow.ProcessWide(new SandboxGlobalConfig { HoldSeconds = -30 }).Value
            .Should().Be(0);
        SandboxHoldWindow.Window(-30).Should().Be(TimeSpan.Zero);
        Resolve(processWide: -30, projectOverride: -600).For(Project).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void HoldWindow_TheProcessWideChain_NamesTheLegThatAnswered()
    {
        Environment.SetEnvironmentVariable(SandboxHoldWindow.EnvironmentVariable, "45");

        SandboxHoldWindow.ProcessWide(new SandboxGlobalConfig { HoldSeconds = 60 })
            .Should().Be(new ResolvedValue<int>(60, ResolutionSource.GlobalDefault));
        SandboxHoldWindow.ProcessWide(new SandboxGlobalConfig())
            .Should().Be(new ResolvedValue<int>(45, ResolutionSource.EnvironmentVariable));

        Environment.SetEnvironmentVariable(SandboxHoldWindow.EnvironmentVariable, null);
        SandboxHoldWindow.ProcessWide(new SandboxGlobalConfig())
            .Should().Be(new ResolvedValue<int>(180, ResolutionSource.CodeDefault));
    }

    /// <summary>
    /// The name mapper used to end in a catch-all arm that rendered anything unrecognised as
    /// the process-wide default's name — so the environment leg, which exists precisely to be
    /// told apart from the settings form, would have claimed to BE it. This fails the moment
    /// a provenance value is added without a name of its own.
    /// </summary>
    [Fact]
    public void ProvenanceNames_EveryResolutionSource_HasANameOfItsOwn()
    {
        var names = Enum.GetValues<ResolutionSource>().ToDictionary(s => s, ResolutionSourceName.Of);

        names.Values.Should().OnlyHaveUniqueItems(
            "a value that borrows another's wire name sends an operator to the wrong place");
        names.Values.Should().NotContainNulls().And.NotContain(string.Empty);
    }

    private static SandboxHoldWindows Resolve(int? processWide, int? projectOverride) =>
        SandboxHoldRailDoubles.Resolver(new SandboxHoldRailDoubles.CountingConfigLoader(new AgentSmithConfig
        {
            Sandbox = new SandboxGlobalConfig { HoldSeconds = processWide },
            Projects = new Dictionary<string, ResolvedProject>(StringComparer.OrdinalIgnoreCase)
            {
                [Project] = new()
                {
                    Name = Project,
                    Sandbox = projectOverride is null ? null : new SandboxConfig { HoldSeconds = projectOverride },
                },
            },
        })).ResolveForScan()!;
}
