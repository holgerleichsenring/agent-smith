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
            && a.Models["coding"].Model == "sonnet-4");
        catalog.Trackers.Should().ContainSingle(t => t.Id == "test-ado" && t.Type == "azure_devops"
            && t.Organization == "testorg" && t.Project == "TestProject" && t.AuthSecret == "token");
        catalog.Repos.Should().ContainSingle(r => r.Id == "test-repo" && r.Name == "https://github.com/test/repo");
        catalog.Projects.Should().ContainSingle(p => p.Id == "testproject" && p.Agent == "claude-default"
            && p.Tracker == "test-ado" && p.Repos.Single() == "test-repo" && p.Pipeline == "fix-bug");
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

        store.UpsertAgent(Agent("new-agent", "openai", "gpt-4.1", "openai_key"), Tester);

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

        store.UpsertAgent(Agent("claude-default", "claude", "opus-4"), Tester);
        store.DeleteAgent("claude-default", Tester);

        var ops = audit.GetAll().Select(c => c.Operation).ToList();
        ops.Should().Equal(ConfigChangeOperation.Delete, ConfigChangeOperation.Update); // newest first
    }

    // p0345 test: AuditRevert_RestoresPriorEntityVersion
    [Fact]
    public void Revert_Update_RestoresPriorAgentModel()
    {
        var (store, audit, _) = NewStore(SampleYaml);

        store.UpsertAgent(Agent("claude-default", "claude", "opus-4"), Tester);
        store.GetAgents().Single(a => a.Id == "claude-default").Models["coding"].Model.Should().Be("opus-4");

        var updateChange = audit.GetAll().First(c => c.Operation == ConfigChangeOperation.Update);
        store.Revert(updateChange.Id, new ChangeAttribution("reverter"));

        store.GetAgents().Single(a => a.Id == "claude-default").Models["coding"].Model.Should().Be("sonnet-4");
        audit.GetById(updateChange.Id)!.Reverted.Should().BeTrue();
    }

    [Fact]
    public void Revert_Create_RemovesTheCreatedEntity()
    {
        var (store, audit, _) = NewStore(SampleYaml);

        store.UpsertTracker(
            new TrackerEntity("gh", "github", "gh_token", Url: "https://github.com/acme/app"), Tester);
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

    // p0345c spec test: ProjectEntity_PipelineRename_RoundTripsAndResolutionValidates
    [Fact]
    public void UpsertProject_PipelineAndResolution_PersistAndReloadThroughRealLoader()
    {
        var (store, _, path) = NewStore(SampleYaml);

        store.UpsertProject(
            new ProjectEntity("p2", "claude-default", "test-ado", ["test-repo"], "add-feature", ["add-feature"],
                new ProjectResolution("area_path", "Acme/Platform")),
            Tester);

        var served = store.GetProjects().Single(p => p.Id == "p2");
        served.Pipeline.Should().Be("add-feature");
        served.Resolution.Should().Be(new ProjectResolution("area_path", "Acme/Platform"));

        // The real loader merges the persisted shorthand into an effective ADO trigger.
        var loaded = RealLoader().LoadConfig(path).Projects["p2"];
        loaded.Pipeline.Should().Be("add-feature");
        loaded.AzuredevopsTrigger!.ProjectResolution!.Strategy.Should().Be(
            AgentSmith.Contracts.Models.Configuration.ResolutionStrategy.AreaPath);
        loaded.AzuredevopsTrigger.ProjectResolution.Value.Should().Be("Acme/Platform");
    }

    [Fact]
    public void UpsertProject_UnknownResolutionStrategy_RejectedAndNotPersisted()
    {
        var (store, _, _) = NewStore(SampleYaml);

        var act = () => store.UpsertProject(
            new ProjectEntity("p2", "claude-default", "test-ado", ["test-repo"], "fix-bug", ["fix-bug"],
                new ProjectResolution("labels", "x")),
            Tester);

        act.Should().Throw<ConfigurationException>().WithMessage("*'labels'*not a known*");
        store.GetProjects().Should().NotContain(p => p.Id == "p2");
    }

    // p0345c: the capabilities descriptor gates tracker writes per type.
    [Fact]
    public void UpsertTracker_MissingPerTypeRequiredField_Rejected()
    {
        var (store, _, _) = NewStore(SampleYaml);

        var act = () => store.UpsertTracker(
            new TrackerEntity("ado2", "azure_devops", "ado_token", Project: "Platform"), Tester);

        act.Should().Throw<ConfigurationException>().WithMessage("*organization*");
    }

    // p0345c spec test: AgentEntity_FullSurface_RoundTripsThroughRealLoader —
    // the bundled operator example's claude-default carries retry/cache/
    // compaction/models/pricing; the studio surfaces all of it, an echoed
    // upsert patches faithfully, and the export still loads via the real loader.
    [Fact]
    public void AgentEntity_FullSurface_RoundTripsThroughRealLoader()
    {
        var examplePath = Path.Combine(AppContext.BaseDirectory, "Configuration", "TestData", "agentsmith.example.yml");
        var (store, _, path) = NewStore(File.ReadAllText(examplePath));

        var agent = store.GetAgents().Single(a => a.Id == "claude-default");
        agent.Models["coding"].Model.Should().Be("claude-sonnet-4-20250514");
        agent.Models["primary"].Should().Be(new AgentModelAssignment("claude-sonnet-4-20250514", null, 8192));
        agent.Models["scout"].Should().Be(new AgentModelAssignment("claude-haiku-4-5-20251001", null, 4096));
        agent.Retry.Should().Be(new AgentRetrySettings(5, 2000, 2.0, 60000));
        agent.Cache.Should().Be(new AgentCacheSettings(true, "automatic"));
        agent.Compaction.Should().Be(new AgentCompactionSettings(true, 8, 80000, 3, "claude-haiku-4-5-20251001"));
        agent.Pricing!.Models["claude-sonnet-4-20250514"].Should().Be(new AgentModelPricing(3.0m, 15.0m, 0.30m));

        // Echo the FULL entity back plus endpoint/api-version/timeout edits.
        store.UpsertAgent(agent with
        {
            Endpoint = "https://llm.example.com",
            ApiVersion = "2025-01-01-preview",
            NetworkTimeoutSeconds = 240,
        }, Tester);

        var loaded = RealLoader().LoadConfig(path).Agents["claude-default"];
        loaded.Endpoint.Should().Be("https://llm.example.com");
        loaded.ApiVersion.Should().Be("2025-01-01-preview");
        loaded.NetworkTimeoutSeconds.Should().Be(240);
        loaded.Model.Should().Be("claude-sonnet-4-20250514");
        loaded.Models!.Primary.MaxTokens.Should().Be(8192);
        loaded.Models.Scout.Model.Should().Be("claude-haiku-4-5-20251001");
        loaded.Retry.MaxRetries.Should().Be(5);
        loaded.Cache.Strategy.Should().Be("automatic");
        loaded.Compaction.MaxContextTokens.Should().Be(80000);
        loaded.Pricing.Models["claude-haiku-4-5-20251001"].OutputPerMillion.Should().Be(4.0m);
        // The parallelism block the studio does NOT surface survives the patch
        // untouched on the sibling agent (claude-parallel).
        RealLoader().LoadConfig(path).Agents["claude-parallel"]
            .Parallelism.MaxConcurrentSkillRounds.Should().Be(4);
    }

    // p0345c: full tracker surface — workflow + polling ride along and reload.
    [Fact]
    public void TrackerEntity_FullSurface_RoundTripsThroughRealLoader()
    {
        var examplePath = Path.Combine(AppContext.BaseDirectory, "Configuration", "TestData", "agentsmith.example.yml");
        var (store, _, path) = NewStore(File.ReadAllText(examplePath));

        var tracker = store.GetTrackers().Single(t => t.Id == "acme-ado");
        tracker.Url.Should().Be("https://dev.azure.com/acme");
        tracker.Organization.Should().Be("acme");
        tracker.OpenStates.Should().Equal("New", "Active");
        tracker.DoneStatus.Should().Be("Resolved");
        tracker.FailedStatus.Should().Be("Resolved");
        tracker.PipelineFromLabel.Should().ContainKey("security").WhoseValue.Should().Be("security-scan");

        // Echo back with edits: label map + polling.
        store.UpsertTracker(tracker with
        {
            PipelineFromLabel = new Dictionary<string, string> { ["bug"] = "fix-bug", ["review"] = "pr-review" },
            Polling = new TrackerPollingSettings(true, 90, 20),
        }, Tester);

        var loaded = RealLoader().LoadConfig(path);
        var raw = store.GetTrackers().Single(t => t.Id == "acme-ado");
        raw.PipelineFromLabel!["review"].Should().Be("pr-review");
        raw.Polling.Should().Be(new TrackerPollingSettings(true, 90, 20));
        // And the merged effective trigger on the routed project sees the tracker workflow.
        loaded.Projects["acme-platform"].AzuredevopsTrigger!.PipelineFromLabel!
            .Should().ContainKey("review");
    }

    private static AgentEntity Agent(string id, string provider, string codingModel, string? keySecret = null) =>
        new(id, provider, keySecret, null, null, null,
            new Dictionary<string, AgentModelAssignment> { ["coding"] = new(codingModel) },
            null, null, null, null);

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
