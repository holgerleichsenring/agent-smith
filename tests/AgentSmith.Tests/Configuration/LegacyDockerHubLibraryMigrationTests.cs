using AgentSmith.Application.Models.Sandbox;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// 2026-11-16-f3fbd: the retired allow_docker_hub_library switch is folded into the list on every load,
/// save and import, and each (switch, list) pair keeps the verdicts it had.
/// </summary>
public sealed class LegacyDockerHubLibraryMigrationTests : IDisposable
{
    private static readonly string[] Images =
        ["node:22-bookworm", "mcr.microsoft.com/dotnet/sdk:8.0", "ghcr.io/o/tool:1", "registry.example.com/x/y:1"];

    private readonly string _yaml = Path.Combine(Path.GetTempPath(), $"agentsmith-library-{Guid.NewGuid():N}.yml");

    public void Dispose()
    {
        if (File.Exists(_yaml)) File.Delete(_yaml);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void Apply_EverySwitchAndList_KeepsTheEffectiveVerdicts(bool? library, bool named)
    {
        var sandbox = Sandbox(library, named);
        var before = Images.Select(i => OldVerdict(library, sandbox.AllowedRegistries, i)).ToList();

        var migrated = new LegacyDockerHubLibraryMigration().Apply(sandbox);

        migrated.AllowDockerHubLibrary.Should().BeNull("the key is dropped in every case");
        Images.Select(i => RegistryTrustBoundary.From(migrated).Accepts(i)).Should().Equal(before);
    }

    [Fact]
    public void Apply_TrueBesideANamedList_AppendsTheLibraryEntry() =>
        new LegacyDockerHubLibraryMigration().Apply(Sandbox(true, true)).AllowedRegistries
            .Should().Equal("registry.example.com/", "docker.io/library/");

    [Fact]
    public void Apply_FalseBesideAnEmptyList_NamesTheOtherDefaults() =>
        new LegacyDockerHubLibraryMigration().Apply(Sandbox(false, false)).AllowedRegistries
            .Should().Equal("mcr.microsoft.com/", "ghcr.io/");

    [Fact]
    public void Assemble_StoredDocumentWithTheSwitch_LoadsMigrated()
    {
        var raw = new ConfigDocumentAssembler().Assemble(
        [
            new ConfigDocRow(ConfigDocTypes.Sandbox, ConfigDocTypes.SingletonId, """{"AllowDockerHubLibrary":false}""", 1),
        ]);

        raw.Sandbox.AllowedRegistries.Should().Equal("mcr.microsoft.com/", "ghcr.io/");
        raw.Sandbox.AllowDockerHubLibrary.Should().BeNull();
    }

    [Fact]
    public void LoadConfig_FileWithTheSwitch_LoadsMigrated()
    {
        File.WriteAllText(_yaml, "sandbox:\n  allowed_registries: [\"ghcr.io/\"]\n  allow_docker_hub_library: true\n");

        var sandbox = new YamlConfigurationLoader(
                new RawConfigMaterializer(
                    new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(),
                    new DeploymentDefaultsApplier(), new ConfigCatalogResolver(), new AgentSmithPaths()),
                new NoOpSystemEventPublisher())
            .LoadConfig(_yaml).Sandbox;

        sandbox.AllowedRegistries.Should().Equal("ghcr.io/", "docker.io/library/");
        sandbox.AllowDockerHubLibrary.Should().BeNull();
    }

    private static SandboxGlobalConfig Sandbox(bool? library, bool named) => new()
    {
        AllowDockerHubLibrary = library,
        AllowedRegistries = named ? ["registry.example.com/"] : [],
    };

    // The boundary as it was before 2026-11-16-f3fbd: named entries replace mcr and ghcr, and the library
    // follows the switch, or — unset — is trusted only while no entry is named.
    private static bool OldVerdict(bool? library, List<string> list, string image)
    {
        IReadOnlyList<string> registries = list.Count > 0 ? list : ["mcr.microsoft.com/", "ghcr.io/"];
        if (registries.Any(r => image.StartsWith(r, StringComparison.Ordinal))) return true;
        return !image.Contains('/') && (library ?? list.Count == 0);
    }
}
