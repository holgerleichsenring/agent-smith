using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Application.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// p0349: the CLI's READ-ONLY file store. It projects agentsmith.yml onto the
/// studio catalog and exports a round-trippable YAML, but every mutation is
/// rejected — editing lives on the server's DbConfigStore, and the file-writeback
/// p0345 shipped (a no-op against a read-only ConfigMap) is gone.
/// </summary>
public sealed class FileConfigStoreTests : IDisposable
{
    private readonly List<string> _tempFiles = new();
    private static readonly ChangeAttribution Tester = new("tester");

    private sealed record FixedLocation(string ConfigPath) : IConfigStoreLocation;

    private (FileConfigStore Store, string Path) NewStore(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-store-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, yaml);
        _tempFiles.Add(path);
        var store = new FileConfigStore(new FixedLocation(path));
        store.Load();
        return (store, path);
    }

    private static YamlConfigurationLoader RealLoader() =>
        new(new RawConfigMaterializer(
                new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(), new DeploymentDefaultsApplier(),
                new ConfigCatalogResolver(), new AgentSmithPaths()),
            new NoOpSystemEventPublisher());

    private const string SampleYaml = """
        agents:
          claude-default:
            type: claude
            model: sonnet-4
        repos:
          test-repo:
            type: github
            url: https://github.com/test/repo
            auth: token
        trackers:
          test-ado:
            type: azure_devops
            organization: testorg
            project: TestProject
            auth: token
        projects:
          testproject:
            agent: claude-default
            tracker: test-ado
            repos: [test-repo]
            pipeline: fix-bug
        secrets:
          github_token: ${AGENTSMITH_TEST_GH_TOKEN}
        """;

    [Fact]
    public void Load_ExistingConfig_ProjectsTheCatalogFaithfully()
    {
        var (store, _) = NewStore(SampleYaml);
        var catalog = store.Catalog;

        catalog.Agents.Should().ContainSingle(a => a.Id == "claude-default" && a.Provider == "claude"
            && a.Models["coding"].Model == "sonnet-4");
        catalog.Trackers.Should().ContainSingle(t => t.Id == "test-ado" && t.Type == "azure_devops"
            && t.Organization == "testorg" && t.Project == "TestProject" && t.AuthSecret == "token");
        catalog.Repos.Should().ContainSingle(r => r.Id == "test-repo" && r.Name == "https://github.com/test/repo");
        catalog.Projects.Should().ContainSingle(p => p.Id == "testproject" && p.Agent == "claude-default"
            && p.Tracker == "test-ado" && p.Repos.Single() == "test-repo" && p.Pipeline == "fix-bug");
        catalog.Secrets.Should().ContainSingle(s => s.Id == "github_token");
    }

    [Fact]
    public void ExportYaml_RoundTripsThroughRealLoader_Unmutated()
    {
        var (store, path) = NewStore(SampleYaml);
        var loader = RealLoader();
        var original = loader.LoadConfig(path);

        var exportedPath = WriteTemp(store.ExportYaml());
        var roundTripped = loader.LoadConfig(exportedPath);

        Project(roundTripped).Should().BeEquivalentTo(Project(original));
    }

    [Fact]
    public void ExportYaml_BundledExample_RoundTripsThroughRealLoader()
    {
        var examplePath = Path.Combine(AppContext.BaseDirectory, "Configuration", "TestData", "agentsmith.example.yml");
        var (store, _) = NewStore(File.ReadAllText(examplePath));
        var loader = RealLoader();
        var original = loader.LoadConfig(examplePath);

        var exportedPath = WriteTemp(store.ExportYaml());
        var roundTripped = loader.LoadConfig(exportedPath);

        Project(roundTripped).Should().BeEquivalentTo(Project(original));
    }

    [Fact]
    public void AgentEntity_FullSurface_ProjectsFromBundledExample()
    {
        var examplePath = Path.Combine(AppContext.BaseDirectory, "Configuration", "TestData", "agentsmith.example.yml");
        var (store, _) = NewStore(File.ReadAllText(examplePath));

        var agent = store.GetAgents().Single(a => a.Id == "claude-default");
        agent.Models["coding"].Model.Should().Be("claude-sonnet-4-20250514");
        agent.Models["primary"].Should().Be(new AgentModelAssignment("claude-sonnet-4-20250514", null, 8192));
        agent.Retry.Should().Be(new AgentRetrySettings(5, 2000, 2.0, 60000));
        agent.Cache.Should().Be(new AgentCacheSettings(true, "automatic"));
        agent.Pricing!.Models["claude-sonnet-4-20250514"].Should().Be(new AgentModelPricing(3.0m, 15.0m, 0.30m));
    }

    [Fact]
    public void FileConfigStore_NoLongerWritesFileBack_ReadOnly()
    {
        var (store, path) = NewStore(SampleYaml);
        var before = File.ReadAllText(path);

        store.Invoking(s => s.UpsertRepo(new RepoEntity("x", "https://github.com/a/b", "main"), Tester))
            .Should().Throw<NotSupportedException>().WithMessage("*read-only*");
        store.Invoking(s => s.DeleteAgent("claude-default", Tester)).Should().Throw<NotSupportedException>();

        File.ReadAllText(path).Should().Be(before, "the read-only store never writes the file back");
    }

    private string WriteTemp(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-export-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    private static object Project(AgentSmith.Contracts.Models.Configuration.AgentSmithConfig config) => new
    {
        Projects = config.Projects.OrderBy(p => p.Key).Select(p => new
        {
            p.Key,
            Agent = p.Value.Agent.Type,
            AgentModel = p.Value.Agent.Model,
            Tracker = p.Value.Tracker.Type,
            p.Value.Tracker.Organization,
            Pipeline = p.Value.Pipeline,
            Repos = p.Value.Repos.Select(r => r.Url).OrderBy(u => u).ToList()
        }).ToList(),
        Trackers = config.Trackers.OrderBy(t => t.Key).Select(t => new { t.Key, t.Value.Type, t.Value.Organization }).ToList(),
        Repos = config.Repos.OrderBy(r => r.Key).Select(r => new { r.Key, r.Value.Type, r.Value.Url }).ToList(),
        Agents = config.Agents.OrderBy(a => a.Key).Select(a => new { a.Key, a.Value.Type, a.Value.Model }).ToList(),
        Secrets = config.Secrets.Keys.OrderBy(k => k).ToList()
    };

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }
}
