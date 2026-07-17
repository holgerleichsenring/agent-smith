using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Application.Services.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.ConfigStudio;

public class FileConfigStoreTests : IDisposable
{
    private readonly List<string> _tempFiles = new();
    private static readonly ChangeAttribution Tester = new("tester");

    private sealed record FixedLocation(string ConfigPath) : IConfigStoreLocation;

    private (FileConfigStore Store, InMemoryConfigAuditStore Audit, string Path) NewStore(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-store-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, yaml);
        _tempFiles.Add(path);
        var audit = new InMemoryConfigAuditStore();
        var store = new FileConfigStore(new FixedLocation(path), audit, NullLogger<FileConfigStore>.Instance);
        store.Load();
        return (store, audit, path);
    }

    private static YamlConfigurationLoader RealLoader() =>
        new(new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(), new DeploymentDefaultsApplier(),
            new ConfigCatalogResolver(), new AgentSmithPaths(), new NoOpSystemEventPublisher());

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

    // p0345 test: FileConfigStore_ExistingConfigs_ParseIdenticallyThroughPort
    [Fact]
    public void Load_ExistingConfig_ProjectsTheCatalogFaithfully()
    {
        var (store, _, _) = NewStore(SampleYaml);
        var catalog = store.Catalog;

        catalog.Agents.Should().ContainSingle(a => a.Id == "claude-default" && a.Provider == "claude"
            && a.Models["coding"] == "sonnet-4");
        catalog.Trackers.Should().ContainSingle(t => t.Id == "test-ado" && t.Type == "azure_devops"
            && t.Org == "testorg" && t.Project == "TestProject" && t.AuthSecret == "token");
        catalog.Repos.Should().ContainSingle(r => r.Id == "test-repo" && r.Name == "https://github.com/test/repo");
        catalog.Projects.Should().ContainSingle(p => p.Id == "testproject" && p.Agent == "claude-default"
            && p.Tracker == "test-ado" && p.Repos.Single() == "test-repo" && p.Trigger == "fix-bug");
        catalog.Secrets.Should().ContainSingle(s => s.Id == "github_token");
    }

    // p0345 test: YamlExport_RoundTrips_ThroughRealLoader
    [Fact]
    public void ExportYaml_RoundTripsThroughRealLoader_Unmutated()
    {
        var (store, _, path) = NewStore(SampleYaml);
        var loader = RealLoader();

        var original = loader.LoadConfig(path);
        var exportedPath = Path.Combine(Path.GetTempPath(), $"agentsmith-export-{Guid.NewGuid():N}.yml");
        _tempFiles.Add(exportedPath);
        File.WriteAllText(exportedPath, store.ExportYaml());

        var roundTripped = loader.LoadConfig(exportedPath);

        Project(roundTripped).Should().BeEquivalentTo(Project(original));
    }

    // p0345 test: same round-trip against the bundled operator example (full-fidelity).
    [Fact]
    public void ExportYaml_BundledExample_RoundTripsThroughRealLoader()
    {
        var examplePath = Path.Combine(AppContext.BaseDirectory, "Configuration", "TestData", "agentsmith.example.yml");
        var (store, _, _) = NewStore(File.ReadAllText(examplePath));
        var loader = RealLoader();

        var original = loader.LoadConfig(examplePath);
        var exportedPath = Path.Combine(Path.GetTempPath(), $"agentsmith-example-export-{Guid.NewGuid():N}.yml");
        _tempFiles.Add(exportedPath);
        File.WriteAllText(exportedPath, store.ExportYaml());

        var roundTripped = loader.LoadConfig(exportedPath);

        Project(roundTripped).Should().BeEquivalentTo(Project(original));
    }

    // p0345 test: DbConfigStore_ProjectWithUnknownAgentRef_RejectedByFkAndApi
    // (FK is the schema skeleton; the runtime rejection is the shared validator,
    //  exercised here through the store — the same guard the API surfaces as 400.)
    [Fact]
    public void UpsertProject_UnknownAgentRef_RejectedAndNotPersisted()
    {
        var (store, _, _) = NewStore(SampleYaml);

        var act = () => store.UpsertProject(
            new ProjectEntity("broken", "no-such-agent", "test-ado", ["test-repo"], "fix-bug", ["fix-bug"]),
            Tester);

        act.Should().Throw<ConfigurationException>().WithMessage("*unknown agent*");
        store.GetProjects().Should().NotContain(p => p.Id == "broken");
    }

    [Fact]
    public void UpsertProject_UnknownRepoRef_Rejected()
    {
        var (store, _, _) = NewStore(SampleYaml);

        var act = () => store.UpsertProject(
            new ProjectEntity("p2", "claude-default", "test-ado", ["ghost-repo"], "fix-bug", ["fix-bug"]),
            Tester);

        act.Should().Throw<ConfigurationException>().WithMessage("*unknown repo*");
    }

    [Fact]
    public void UpsertProject_AllRefsKnown_Persists()
    {
        var (store, _, _) = NewStore(SampleYaml);

        store.UpsertProject(
            new ProjectEntity("p2", "claude-default", "test-ado", ["test-repo"], "fix-bug", ["fix-bug"]),
            Tester);

        store.GetProjects().Should().ContainSingle(p => p.Id == "p2");
    }

    // p0345 test: AuditTable_EveryMutation_WritesAttributedDiffRow
    [Fact]
    public void EveryMutation_WritesAttributedDiffRow()
    {
        var (store, audit, _) = NewStore(SampleYaml);

        store.UpsertAgent(new AgentEntity("new-agent", "openai",
            new Dictionary<string, string> { ["coding"] = "gpt-4.1" }, "openai_key"), Tester);

        var change = audit.GetAll().Should().ContainSingle().Subject;
        change.Actor.Should().Be("tester");
        change.EntityType.Should().Be(ConfigEntityType.Agent);
        change.EntityId.Should().Be("new-agent");
        change.Operation.Should().Be(ConfigChangeOperation.Create);
        change.BeforeJson.Should().BeNull();
        change.AfterJson.Should().NotBeNull().And.Subject.Should().Contain("openai");
        change.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void UpdateThenDelete_RecordsUpdateAndDeleteRows()
    {
        var (store, audit, _) = NewStore(SampleYaml);

        store.UpsertAgent(new AgentEntity("claude-default", "claude",
            new Dictionary<string, string> { ["coding"] = "opus-4" }, null), Tester);
        store.DeleteAgent("claude-default", Tester);

        var ops = audit.GetAll().Select(c => c.Operation).ToList();
        ops.Should().Equal(ConfigChangeOperation.Delete, ConfigChangeOperation.Update); // newest first
    }

    // p0345 test: AuditRevert_RestoresPriorEntityVersion
    [Fact]
    public void Revert_Update_RestoresPriorAgentModel()
    {
        var (store, audit, _) = NewStore(SampleYaml);

        store.UpsertAgent(new AgentEntity("claude-default", "claude",
            new Dictionary<string, string> { ["coding"] = "opus-4" }, null), Tester);
        store.GetAgents().Single(a => a.Id == "claude-default").Models["coding"].Should().Be("opus-4");

        var updateChange = audit.GetAll().First(c => c.Operation == ConfigChangeOperation.Update);
        store.Revert(updateChange.Id, new ChangeAttribution("reverter"));

        store.GetAgents().Single(a => a.Id == "claude-default").Models["coding"].Should().Be("sonnet-4");
        audit.GetById(updateChange.Id)!.Reverted.Should().BeTrue();
    }

    [Fact]
    public void Revert_Create_RemovesTheCreatedEntity()
    {
        var (store, audit, _) = NewStore(SampleYaml);

        store.UpsertTracker(new TrackerEntity("gh", "github", "acme", null, "gh_token"), Tester);
        var create = audit.GetAll().First(c => c.EntityId == "gh");

        store.Revert(create.Id, Tester);

        store.GetTrackers().Should().NotContain(t => t.Id == "gh");
    }

    // p0345 test: Secrets_OnlyEnvNamesPersisted_NeverValues
    [Fact]
    public void UpsertSecret_PersistsEnvNameReferenceOnly_NeverAValue()
    {
        var (store, _, path) = NewStore(SampleYaml);

        store.UpsertSecret(new SecretEntity("openai_api_key"), Tester);

        // The catalog view exposes the name only.
        store.GetSecrets().Should().ContainSingle(s => s.Id == "openai_api_key");

        // The persisted file stores a ${ENV} placeholder — never a resolved value.
        var written = File.ReadAllText(path);
        written.Should().Contain("openai_api_key: ${openai_api_key}");
    }

    [Fact]
    public void Mutation_PersistsToFile_ReloadableThroughRealLoader()
    {
        var (store, _, path) = NewStore(SampleYaml);

        store.UpsertRepo(new RepoEntity("second-repo", "https://github.com/test/other", "main"), Tester);

        // A fresh store over the same file sees the persisted mutation.
        var reloaded = new FileConfigStore(new FixedLocation(path), new InMemoryConfigAuditStore(),
            NullLogger<FileConfigStore>.Instance);
        reloaded.Load().Repos.Should().Contain(r => r.Id == "second-repo");

        // And the file still loads through the real loader.
        RealLoader().LoadConfig(path).Repos.Should().ContainKey("second-repo");
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
