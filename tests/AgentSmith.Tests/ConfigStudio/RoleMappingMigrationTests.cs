using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Startup;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-08-25-1806: an installation whose role mapping is in its file has a WORKING mapping
/// today, so the release that moves the mapping into the store imports it rather than
/// dropping it. Once the store holds one, the store is the single answer — a second boot
/// must not re-import the file over an edit made in the studio.
/// </summary>
public sealed class RoleMappingMigrationTests
{
    [Fact]
    public void Migration_FileHasAMapping_ItIsImportedOnce()
    {
        using var harness = new DbConfigTestHarness();
        var auth = FileMapping();

        Migration(harness, auth).Run();

        Stored(harness)!.Roles.Should().ContainKey("auditor");
        Stored(harness)!.GroupRoles.Should().ContainKey("platform-admins");
        Stored(harness)!.RoleClaim.Should().Be("app_roles");
    }

    [Fact]
    public void Migration_StoreAlreadyHasOne_TheFileDoesNotOverwriteIt()
    {
        using var harness = new DbConfigTestHarness();
        Save(harness, new RoleMappingConfig
        {
            Roles = new Dictionary<string, List<string>> { ["studio-role"] = ["runs.read"] },
        });

        Migration(harness, FileMapping()).Run();

        var stored = Stored(harness)!;
        stored.Roles.Should().ContainKey("studio-role");
        stored.Roles.Should().NotContainKey("auditor", "the studio's copy is the newer one");
    }

    [Fact]
    public void Migration_FileHasNoMapping_NothingIsWrittenAndTheStoreAnswers()
    {
        using var harness = new DbConfigTestHarness();

        Migration(harness, new TokenAuthorityConfig()).Run();

        harness.DocStore.LoadAll().Should().NotContain(row => row.Type == ConfigDocTypes.RoleMapping,
            "a file that declares no mapping has nothing to preserve");
    }

    [Fact]
    public void Migration_StoreUnreachable_RecordsAFindingAndLeavesTheFileInForce()
    {
        using var harness = new DbConfigTestHarness();
        var auth = FileMapping();
        var source = new RoleMappingSource(new UnreadableStoredMapping(), auth);
        var findings = new StartupFindings();
        new RoleMappingMigration(
            new UnreachableDocuments(), auth, source, new ConfigDocJson(), findings,
            NullLogger<RoleMappingMigration>.Instance).Run();

        findings.All.Should().ContainSingle().Which.Reason.Should().Contain("role mapping");
        source.Current().Mapping.Roles.Should().ContainKey("auditor");
    }

    [Fact]
    public void Bootstrap_AuthorityAudienceAndEnforce_StayInTheFileAndTheEnvironment()
    {
        using var harness = new DbConfigTestHarness();
        harness.Import(
            "auth:\n  authority: https://issuer.example/realm\n  audience: agent-smith\n  enforce: true\n");

        harness.Store.SettingTypes.Should().NotContain("auth");
        harness.Assembler.Assemble(harness.DocStore.LoadAll()).Auth.Should().BeNull(
            "the pipeline that validates a token is registered before the database exists");
    }

    [Fact]
    public void Mapping_Saved_AppliesToTheNextRequestWithoutARestart()
    {
        using var harness = new DbConfigTestHarness();
        var auth = new TokenAuthorityConfig();
        var source = new RoleMappingSource(
            new StoredRoleMapping(harness.Store, NullLogger<StoredRoleMapping>.Instance), auth);
        source.AdoptStore();
        var resolver = new CallerIdentityResolver(source, Server.Auth.ResolverUnderTest.Grant(null));
        var caller = Server.Auth.ResolverUnderTest.Caller(auth, ("roles", "auditor"));

        resolver.Resolve(caller).Permissions.Should().NotContain("config.read");

        harness.Store.SaveSetting(ConfigDocTypes.RoleMapping,
            Doc("""{"roleClaim":"roles","groupClaim":"groups","roles":{"auditor":["config.read"]},"groupRoles":{}}"""),
            new ChangeAttribution("alice"));

        resolver.Resolve(caller).Permissions.Should().Contain("config.read",
            "the same process, no restart, one call later");
    }

    private static RoleMappingMigration Migration(DbConfigTestHarness harness, TokenAuthorityConfig auth) =>
        new(harness.DocStore, auth,
            new RoleMappingSource(
                new StoredRoleMapping(harness.Store, NullLogger<StoredRoleMapping>.Instance), auth),
            new ConfigDocJson(), new StartupFindings(), NullLogger<RoleMappingMigration>.Instance);

    private static RoleMappingConfig? Stored(DbConfigTestHarness harness)
    {
        harness.Store.Load();
        return harness.Store.GetSetting(ConfigDocTypes.RoleMapping) as RoleMappingConfig;
    }

    private static void Save(DbConfigTestHarness harness, RoleMappingConfig mapping) =>
        harness.DocStore.Save(new ConfigDocWrite(
            ConfigDocTypes.RoleMapping, ConfigDocTypes.SingletonId,
            JsonSerializer.Serialize(mapping, new ConfigDocJson().Options),
            null, [], "studio"));

    private static TokenAuthorityConfig FileMapping() => new()
    {
        RoleClaim = "app_roles",
        Roles = new Dictionary<string, List<string>> { ["auditor"] = ["config.read"] },
        GroupRoles = new Dictionary<string, List<string>> { ["platform-admins"] = ["admin"] },
    };

    private static JsonElement Doc(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
