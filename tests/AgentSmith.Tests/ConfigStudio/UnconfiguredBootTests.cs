using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// p0349: an empty store boots the server UNCONFIGURED — the studio is reachable to
/// enter config (DbConfigStore serves an empty catalog without throwing), and the
/// DB configuration loader yields an empty-but-valid config so pollers/pipelines
/// have nothing to act on until a minimal-viable config is imported.
/// </summary>
public sealed class UnconfiguredBootTests : IDisposable
{
    private readonly DbConfigTestHarness _h = new();
    private readonly List<string> _tempFiles = new();

    private const string MinimalViableYaml = """
        agents:
          a1: { type: claude, model: sonnet-4 }
        connections:
          c1: { type: github, owner: acme, auth: gh }
        trackers:
          t1: { type: github, organization: acme, auth: gh }
        projects:
          p1:
            agent: a1
            tracker: t1
            repos: [c1/App]
            pipeline: fix-bug
        secrets:
          gh: ${AGENTSMITH_TEST_GH}
        """;

    [Fact]
    public void ServerBoot_EmptyDb_StudioReachable_PipelinesIdleUntilMinimalConfig()
    {
        var loader = BuildLoader();

        // Empty store: the studio is reachable (no throw, empty catalog) and the
        // loaded config has no trackers/projects, so pollers/pumps stay idle.
        _h.DocStore.IsEmpty().Should().BeTrue();
        _h.Store.GetAgents().Should().BeEmpty("the studio serves an empty catalog, not an error");
        var unconfigured = loader.LoadConfig("ignored");
        unconfigured.Trackers.Should().BeEmpty();
        unconfigured.Projects.Should().BeEmpty();

        // Import a minimal-viable config (>=1 agent + connection + tracker + project):
        // the very same loader now yields a fully wired config.
        _h.Import(MinimalViableYaml);
        var configured = loader.LoadConfig("ignored");
        configured.Trackers.Should().ContainKey("t1");
        configured.Projects.Should().ContainKey("p1");
    }

    private DbConfigurationLoader BuildLoader()
    {
        var bootstrapPath = Path.Combine(Path.GetTempPath(), $"agentsmith-boot-{Guid.NewGuid():N}.yml");
        File.WriteAllText(bootstrapPath, "persistence:\n  provider: sqlite\n  connection_string: 'Data Source=x.db'\n");
        _tempFiles.Add(bootstrapPath);
        var materializer = new RawConfigMaterializer(
            new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(), new DeploymentDefaultsApplier(),
            new ConfigCatalogResolver(), new AgentSmithPaths());
        var bootstrap = new BootstrapConfigReader(new FixedLocation(bootstrapPath));
        return new DbConfigurationLoader(_h.DocStore, _h.Assembler, materializer, bootstrap);
    }

    private sealed record FixedLocation(string ConfigPath) : IConfigStoreLocation;

    public void Dispose()
    {
        _h.Dispose();
        foreach (var f in _tempFiles) if (File.Exists(f)) File.Delete(f);
    }
}
