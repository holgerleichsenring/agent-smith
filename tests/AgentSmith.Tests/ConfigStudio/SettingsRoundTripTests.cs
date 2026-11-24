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
            "dialogue", "deployment", "registries", "primary_provider",
            // 2026-08-25-1806: what a role name means is application configuration too.
            "pipeline_data_flow", "role_mapping", "trace",
            "spec_review", // 2026-10-13-8762g
            "series", // 2026-10-13-8762u
            "preconditions", // 2026-11-16-f3fbc
            "branch_cleanup", // 2026-11-17-713ec
            "tickets", // 2026-11-17-f9b0d
            "holds", // 2026-11-23-31dci
            "pocs", // 2026-11-23-933da
        });
        h.Store.SettingTypes.Should().NotContain("persistence").And.NotContain("secret");
    }

    [Fact]
    public void SaveSetting_FlatDoc_RoundTripsAndRecordsAttributedVersion()
    {
        using var h = new DbConfigTestHarness();
        h.Import("orchestrator:\n  max_run_wall_time_seconds: 1800\n");

        h.Store.SaveSetting("orchestrator",
            Doc("""{"maxRunWallTimeSeconds":5400}"""),
            new ChangeAttribution("alice"));

        // The assembled runtime value is the saved one.
        var raw = h.Assembler.Assemble(h.DocStore.LoadAll());
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

    // 2026-11-23-31dci: the escalation's field saves and the runtime reads the saved minutes.
    [Fact]
    public void SaveSetting_Holds_RoundTripsTheEscalationMinutes()
    {
        using var h = new DbConfigTestHarness();

        h.Store.SaveSetting("holds", Doc("""{"escalateAfterMinutes":0}"""), new ChangeAttribution("op"));

        h.Assembler.Assemble(h.DocStore.LoadAll()).Holds.EscalateAfterMinutes.Should().Be(0);
        Wire(h.Store.GetSetting("holds")).GetProperty("escalateAfterMinutes").GetInt32().Should().Be(0);
    }

    // 2026-11-23-933da: the PoCs form saves the switch and a target, and the runtime reads them back.
    [Fact]
    public void SaveSetting_Pocs_RoundTripsTheSwitchAndATarget()
    {
        using var h = new DbConfigTestHarness();

        h.Store.SaveSetting("pocs", Doc("""
            {"enabled":true,"projectAgent":"a","projectTracker":"t","targets":[{"name":"main","sourceRepo":"gh/poc-src",
            "domainPattern":"{app}.poc.example.internal","reminderDays":[7,1],"perPersonLimit":2,
            "registry":{"type":"acr","loginServer":"pocregistry.azurecr.io","pullUser":"poc-pull","pullAuth":"poc_pull","pullMode":"cluster_pulls"}}],
            "scan":{"osvImage":"ghcr.io/google/osv-scanner@sha256:f7ba4be68bac8086b1f88fd598fdca1ca67239c79ad2c2b5c78e03a82e5187c4","trivyImage":"aquasec/trivy@sha256:0000000000000000000000000000000000000000000000000000000000000933"}}
            """), new ChangeAttribution("op"));

        var pocs = h.Assembler.Assemble(h.DocStore.LoadAll()).Pocs;
        pocs.Enabled.Should().BeTrue();
        var target = pocs.Targets.Should().ContainSingle().Subject;
        (target.SourceRepo, target.Root, target.PerPersonLimit).Should().Be(("gh/poc-src", "pocs", 2));
        target.ReminderDays.Should().Equal(7, 1);
        (target.Registry!.PullAuth, target.Registry.PullMode, target.Registry.Repository).Should().Be(("poc_pull", "cluster_pulls", "pocs"));
        Wire(h.Store.GetSetting("pocs")).GetProperty("targets")[0].GetProperty("sourceRepo").GetString().Should().Be("gh/poc-src");
        Wire(h.Store.GetSetting("pocs")).GetProperty("targets")[0].GetProperty("registry").GetProperty("pullAuth").GetString()
            .Should().Be("poc_pull");
    }

    // 2026-11-23-933de: a target without a registry block, or with a bad one, is never saved.
    [Theory]
    [InlineData("", "pocs.targets[].registry of target 'main' is missing")]
    [InlineData(""","registry":{"loginServer":"registry.example.com","pullUser":"u","pullAuth":"s"}""", "pocs.targets[].registry.login_server")]
    public void SaveSetting_PocsTargetWithoutOrWithABadRegistry_IsRefusedNamingTheKey(string registry, string expected)
    {
        using var h = new DbConfigTestHarness();

        var save = () => h.Store.SaveSetting("pocs", Doc($$"""
            {"enabled":false,"targets":[{"name":"main","sourceRepo":"gh/poc-src"{{registry}}}]}
            """), new ChangeAttribution("op"));

        save.Should().Throw<AgentSmith.Domain.Exceptions.ConfigurationException>().WithMessage($"*{expected}*");
    }

    // 2026-11-23-933db: the archive limits ride in the pocs singleton; a block without them reads as the defaults.
    [Fact]
    public void SaveSetting_PocsArchive_RoundTripsItsLimits_AndItsAbsenceReadsAsTheDefaults()
    {
        using var h = new DbConfigTestHarness();

        h.Store.SaveSetting("pocs", Doc("""{"enabled":false,"archive":{"maxZipMb":30,"mismatchFileLimit":7}}"""),
            new ChangeAttribution("op"));
        var archive = h.Assembler.Assemble(h.DocStore.LoadAll()).Pocs.Archive;
        (archive.MaxZipMb, archive.MismatchFileLimit, archive.MaxEntries).Should().Be((30, 7, 2000));
        Wire(h.Store.GetSetting("pocs")).GetProperty("archive").GetProperty("maxZipMb").GetInt32().Should().Be(30);

        h.Store.SaveSetting("pocs", Doc("""{"enabled":false}"""), new ChangeAttribution("op"));
        h.Assembler.Assemble(h.DocStore.LoadAll()).Pocs.Archive.MaxZipMb.Should().Be(25);
    }

    // 2026-11-23-933dc: the scan settings and the blocked retention ride in the pocs singleton too.
    [Fact]
    public void SaveSetting_PocsScan_RoundTripsItsValues_AndItsAbsenceReadsAsTheDefaults()
    {
        using var h = new DbConfigTestHarness();

        h.Store.SaveSetting("pocs", Doc("""{"enabled":false,"blockedRetentionDays":3,"scan":{"timeoutSeconds":120,"unfixedCriticalBlock":false,"secretMinConfidence":9}}"""),
            new ChangeAttribution("op"));
        var pocs = h.Assembler.Assemble(h.DocStore.LoadAll()).Pocs;
        (pocs.BlockedRetentionDays, pocs.Scan.TimeoutSeconds, pocs.Scan.UnfixedCriticalBlock, pocs.Scan.SecretMinConfidence)
            .Should().Be((3, 120, false, 9));
        Wire(h.Store.GetSetting("pocs")).GetProperty("scan").GetProperty("secretMinConfidence").GetInt32().Should().Be(9);

        h.Store.SaveSetting("pocs", Doc("""{"enabled":false}"""), new ChangeAttribution("op"));
        var defaults = h.Assembler.Assemble(h.DocStore.LoadAll()).Pocs;
        (defaults.BlockedRetentionDays, defaults.Scan.SecretMinConfidence).Should().Be((7, 8));
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
              "perPipeline": { "code": { "usd": 3.0, "tokens": 300000 } },
              "perTier": { "Large": { "usd": 40.0, "tokens": 8000000 } }
            }
            """), new ChangeAttribution("op"));

        var cap = h.Assembler.Assemble(h.DocStore.LoadAll()).PipelineCostCap;
        cap.Default.Usd.Should().Be(6.0m);
        cap.PerPipeline["code"].Tokens.Should().Be(300_000);
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

    // 2026-09-18-0f27: a settings save binds the client's payload onto a FRESH scratch
    // object and reads the whole value back, so every field the client omits lands at its
    // model default. For the concurrent-sandbox bound that default is NULL — which is what
    // keeps SANDBOX_MAX_CONCURRENT reachable. Had the field been a plain int defaulting to
    // 2, the first save of an unrelated sandbox setting would have stored a bound nobody
    // chose and the variable could never be read again.
    [Fact]
    public void Bound_ASaveThatDidNotTouchIt_LeavesTheFallbackStanding()
    {
        using var h = new DbConfigTestHarness();

        h.Store.SaveSetting("sandbox",
            Doc("""{"agentRegistry":"ghcr.io/sample","stepTimeoutSeconds":900,"runCommandTimeoutSeconds":300}"""),
            new ChangeAttribution("op"));

        var sandbox = h.Assembler.Assemble(h.DocStore.LoadAll()).Sandbox;
        sandbox.StepTimeoutSeconds.Should().Be(900);
        sandbox.MaxConcurrentSandboxes.Should().BeNull("an omitted bound is nobody's choice, not a stored 2");
        Wire(h.Store.GetSetting("sandbox")).GetProperty("maxConcurrentSandboxes")
            .ValueKind.Should().Be(JsonValueKind.Null);
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
