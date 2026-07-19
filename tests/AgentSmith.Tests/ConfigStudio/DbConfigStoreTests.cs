using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// p0349: the server's DB entity-document store. Config is DB rows, not the file;
/// the studio edits persist, the single audit is config_entity_version, and the
/// reference graph + secret + concurrency invariants hold.
/// </summary>
public sealed class DbConfigStoreTests : IDisposable
{
    private readonly DbConfigTestHarness _h = new();
    private static readonly ChangeAttribution Tester = new("tester");

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
        limits:
          max_tool_calls_per_skill: 42
        secrets:
          github_token: ${AGENTSMITH_TEST_GH_TOKEN}
        """;

    [Fact]
    public void DbConfigStore_RoundTripsAgentSmithConfig_IdenticalToFileStoreThroughPort()
    {
        _h.Import(SampleYaml);
        var fileCatalog = FileStoreCatalog(SampleYaml);

        _h.Store.Catalog.Should().BeEquivalentTo(fileCatalog);
    }

    [Fact]
    public void Assembly_CollectionAndSingletonTypes_RoundTripToTypedConfig()
    {
        _h.Import(SampleYaml);

        // The singleton (limits) AND the collection entries survive DB -> YAML -> real loader.
        var reloaded = RealLoader().LoadConfig(WriteTemp(_h.Store.ExportYaml()));
        reloaded.Limits.MaxToolCallsPerSkill.Should().Be(42);
        reloaded.Agents.Should().ContainKey("claude-default");
        reloaded.Projects.Should().ContainKey("testproject");
    }

    [Fact]
    public void ConfigEntityVersion_IsTheSingleAuditPath_AttributedAndRevertible()
    {
        _h.Import(SampleYaml);

        _h.Store.UpsertAgent(Agent("claude-default", "claude", "opus-4"), Tester);
        _h.Store.GetAgents().Single(a => a.Id == "claude-default").Models["coding"].Model.Should().Be("opus-4");

        var update = _h.Store.GetChanges().First(c =>
            c.EntityId == "claude-default" && c.Operation == ConfigChangeOperation.Update);
        update.Actor.Should().Be("tester");

        _h.Store.Revert(update.Id, new ChangeAttribution("reverter"));
        _h.Store.GetAgents().Single(a => a.Id == "claude-default").Models["coding"].Model.Should().Be("sonnet-4");
    }

    [Fact]
    public void ConfigRef_DeleteReferencedEntity_RejectedWithReferencingSet()
    {
        _h.Import(SampleYaml);

        var act = () => _h.Store.DeleteAgent("claude-default", Tester);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*referenced by*")
            .WithMessage("*project/testproject*");
        _h.Store.GetAgents().Should().Contain(a => a.Id == "claude-default");
    }

    [Fact]
    public void SaveDoc_WithRawSecretValue_Rejected_NamesOnly()
    {
        var rawValue = new ConfigDocWrite("secret", "github_token", "\"ghp_live_secret_123\"", null, [], "tester");
        var envName = new ConfigDocWrite("secret", "github_token", "\"${GITHUB_TOKEN}\"", null, [], "tester");

        _h.Invoking(h => h.DocStore.Save(rawValue)).Should().Throw<ConfigurationException>()
            .WithMessage("*raw value*");
        _h.DocStore.Invoking(d => d.Save(envName)).Should().NotThrow();
    }

    [Fact]
    public void StaleVersionWrite_Rejected_409()
    {
        _h.DocStore.Save(new ConfigDocWrite("agent", "a1", "{\"type\":\"claude\"}", null, [], "tester"));

        // A second write claiming the entity is still at version 0 is stale.
        var stale = new ConfigDocWrite("agent", "a1", "{\"type\":\"openai\"}", 0, [], "tester");

        _h.DocStore.Invoking(d => d.Save(stale)).Should().Throw<StaleConfigVersionException>();
    }

    [Fact]
    public void NewSettingField_AddedToDoc_RequiresNoDbMigration()
    {
        // A doc carrying a field the C# model does not know today persists as opaque
        // JSON with zero schema change — the whole point of the doc store.
        var doc = "{\"maxIterations\":7,\"aBrandNewSettingAddedLater\":true}";
        _h.DocStore.Save(new ConfigDocWrite("limits", "default", doc, null, [], "tester"));

        _h.DocStore.LoadAll().Should().ContainSingle(r => r.Type == "limits" && r.Doc.Contains("aBrandNewSettingAddedLater"));
    }

    private ConfigCatalog FileStoreCatalog(string yaml)
    {
        var path = WriteTemp(yaml);
        var store = new FileConfigStore(new FixedLocation(path));
        return store.Load();
    }

    private static AgentSmith.Infrastructure.Core.Services.Configuration.YamlConfigurationLoader RealLoader() =>
        new(new AgentSmith.Infrastructure.Core.Services.Configuration.RawConfigMaterializer(
                new AgentSmith.Infrastructure.Core.Services.Configuration.ProjectConfigNormalizer(),
                new AgentSmith.Infrastructure.Core.Services.Configuration.EffectiveTriggerBuilder(),
                new AgentSmith.Infrastructure.Core.Services.Configuration.DeploymentDefaultsApplier(),
                new AgentSmith.Infrastructure.Core.Services.Configuration.ConfigCatalogResolver(),
                new AgentSmith.Infrastructure.Core.Services.AgentSmithPaths()),
            new AgentSmith.Application.Services.Events.NoOpSystemEventPublisher());

    private readonly List<string> _tempFiles = new();

    private string WriteTemp(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-db-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    private static AgentEntity Agent(string id, string provider, string codingModel) =>
        new(id, provider, null, null, null, null,
            new Dictionary<string, AgentModelAssignment> { ["coding"] = new(codingModel) },
            // p0351: every routed model must be priced — ValidateAgent enforces it on upsert.
            new AgentPricing(new Dictionary<string, AgentModelPricing> { [codingModel] = new(0m, 0m) }),
            null, null, null);

    private sealed record FixedLocation(string ConfigPath) : IConfigStoreLocation;

    public void Dispose()
    {
        _h.Dispose();
        foreach (var f in _tempFiles) if (File.Exists(f)) File.Delete(f);
    }
}
