using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// p0353: the global SETTINGS singletons as an editable studio surface. Proves the
/// generic read/save path over the real DB stack — a saved settings doc round-trips
/// through the assembler, records an attributed + revertible change, and each save
/// is a fresh version (the epoch the poller/enforcers re-read from).
/// </summary>
public sealed class SettingsRoundTripTests
{
    [Fact]
    public void SettingTypes_ExposesEverySingletonExceptBootstrapPersistence()
    {
        using var h = new DbConfigTestHarness();

        h.Store.SettingTypes.Should().BeEquivalentTo(new[]
        {
            "orchestrator", "limits", "pipeline_cost_cap", "skills", "sandbox", "queue",
            "dialogue", "deployment", "registries", "primary_provider", "pipeline_storage",
            "pipeline_data_flow",
        });
        h.Store.SettingTypes.Should().NotContain("persistence").And.NotContain("secret");
    }

    [Fact]
    public void SaveSetting_FlatDoc_RoundTripsAndRecordsAttributedVersion()
    {
        using var h = new DbConfigTestHarness();
        h.Import("orchestrator:\n  max_run_wall_time_seconds: 1800\n");

        h.Store.SaveSetting("orchestrator",
            Doc("""{"registry":"ghcr.io/sample","version":"1.2.3","maxRunWallTimeSeconds":5400}"""),
            new ChangeAttribution("alice"));

        // The assembled runtime value is the saved one.
        var raw = h.Assembler.Assemble(h.DocStore.LoadAll());
        raw.Orchestrator.Registry.Should().Be("ghcr.io/sample");
        raw.Orchestrator.MaxRunWallTimeSeconds.Should().Be(5400);

        // GetSetting reads the same value back, typed — serialized the same camelCase
        // way the endpoint puts it on the wire.
        var read = Wire(h.Store.GetSetting("orchestrator"));
        read.GetProperty("maxRunWallTimeSeconds").GetInt32().Should().Be(5400);

        // The save is an attributed, versioned audit row.
        h.DocStore.GetVersions().Should().Contain(v => v.Type == "orchestrator" && v.ChangedBy == "alice");
    }

    [Fact]
    public void SaveSetting_ShowsInChanges_AsSettingsKindKeyedByType()
    {
        using var h = new DbConfigTestHarness();

        h.Store.SaveSetting("orchestrator", Doc("""{"maxRunWallTimeSeconds":5400}"""),
            new ChangeAttribution("alice"));

        var change = h.Store.GetChanges().Should()
            .ContainSingle(c => c.EntityType == ConfigEntityType.Settings).Subject;
        change.EntityId.Should().Be("orchestrator");
        change.Actor.Should().Be("alice");
        change.Operation.Should().Be(ConfigChangeOperation.Create);
        change.AfterJson.Should().Contain("5400");
    }

    [Fact]
    public void Revert_SettingsChange_RestoresPriorDoc_AndBumpsVersion()
    {
        using var h = new DbConfigTestHarness();
        h.Store.SaveSetting("queue", Doc("""{"maxParallelJobs":8}"""), new ChangeAttribution("op"));
        h.Store.SaveSetting("queue", Doc("""{"maxParallelJobs":12}"""), new ChangeAttribution("op"));
        h.Assembler.Assemble(h.DocStore.LoadAll()).Queue.MaxParallelJobs.Should().Be(12);

        // Revert the second (Update) settings change — the before-doc (8) is written back.
        var second = h.Store.GetChanges()
            .Where(c => c.EntityType == ConfigEntityType.Settings && c.EntityId == "queue")
            .OrderByDescending(c => c.Version).First();
        h.Store.Revert(second.Id, new ChangeAttribution("op"));

        h.Assembler.Assemble(h.DocStore.LoadAll()).Queue.MaxParallelJobs.Should().Be(8);
        // Revert appends a fresh version — the epoch the poller/enforcers re-read from.
        h.DocStore.GetVersions().Where(v => v.Type == "queue").Should().HaveCount(3);
    }

    [Fact]
    public void SaveSetting_EachSaveIsAFreshVersion_TheEpochEnforcersReadFrom()
    {
        using var h = new DbConfigTestHarness();

        h.Store.SaveSetting("queue", Doc("""{"maxParallelJobs":8}"""), new ChangeAttribution("op"));
        h.Store.SaveSetting("queue", Doc("""{"maxParallelJobs":12}"""), new ChangeAttribution("op"));

        // Two saves → two versions of the one 'default' doc (the version bump is what
        // the config epoch signal carries to the poller and settings enforcers).
        var versions = h.DocStore.GetVersions().Where(v => v.Type == "queue").ToList();
        versions.Should().HaveCount(2);
        h.Assembler.Assemble(h.DocStore.LoadAll()).Queue.MaxParallelJobs.Should().Be(12);
    }

    [Fact]
    public void SaveSetting_NestedCostCapDoc_RoundTripsDefaultPerPipelineAndPerTier()
    {
        using var h = new DbConfigTestHarness();

        h.Store.SaveSetting("pipeline_cost_cap", Doc("""
            {
              "default": { "usd": 6.0, "tokens": 600000 },
              "perPipeline": { "fix-bug": { "usd": 3.0, "tokens": 300000 } },
              "perTier": { "Large": { "usd": 40.0, "tokens": 8000000 } }
            }
            """), new ChangeAttribution("op"));

        var cap = h.Assembler.Assemble(h.DocStore.LoadAll()).PipelineCostCap;
        cap.Default.Usd.Should().Be(6.0m);
        cap.PerPipeline["fix-bug"].Tokens.Should().Be(300_000);
        cap.PerTier[ComplexityTier.Large].Usd.Should().Be(40.0m);
    }

    [Fact]
    public void SaveSetting_ScalarPrimaryProvider_RoundTrips()
    {
        using var h = new DbConfigTestHarness();

        h.Store.SaveSetting("primary_provider", Doc("""{"value":"claude-default"}"""), new ChangeAttribution("op"));

        h.Assembler.Assemble(h.DocStore.LoadAll()).PrimaryProvider.Should().Be("claude-default");
        Wire(h.Store.GetSetting("primary_provider")).GetProperty("value").GetString().Should().Be("claude-default");
    }

    [Fact]
    public void SaveSetting_ListRegistries_RoundTrips()
    {
        using var h = new DbConfigTestHarness();

        h.Store.SaveSetting("registries",
            Doc("""[{"host":"pkgs.dev.azure.com","username":"any","token":"${feed_token}"}]"""),
            new ChangeAttribution("op"));

        var registries = h.Assembler.Assemble(h.DocStore.LoadAll()).Registries;
        registries.Should().ContainSingle().Which.Host.Should().Be("pkgs.dev.azure.com");
    }

    [Fact]
    public void SaveSetting_UnknownType_Throws()
    {
        using var h = new DbConfigTestHarness();

        var act = () => h.Store.SaveSetting("nope", Doc("{}"), new ChangeAttribution("op"));

        act.Should().Throw<ConfigurationException>();
    }

    [Fact]
    public void SaveSetting_Persistence_IsNotEditable()
    {
        using var h = new DbConfigTestHarness();

        h.Store.SettingTypes.Should().NotContain("persistence");
        var act = () => h.Store.SaveSetting("persistence", Doc("{}"), new ChangeAttribution("op"));
        act.Should().Throw<ConfigurationException>();
    }

    private static JsonElement Doc(string json) => JsonDocument.Parse(json).RootElement.Clone();

    // Serialize the typed settings value the same camelCase way the endpoint emits it.
    private static JsonElement Wire(object value) =>
        JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
