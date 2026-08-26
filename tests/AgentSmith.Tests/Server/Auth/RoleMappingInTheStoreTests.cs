using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-08-25-1806: what a role means comes from the config store, not from a file a pod
/// has to restart to read. The bootstrap block is the SEED an installation that has not
/// migrated is served from, and the store is the one answer once it has spoken.
/// </summary>
public sealed class RoleMappingInTheStoreTests
{
    [Fact]
    public void Store_HoldsAMapping_ItIsWhatResolves()
    {
        // The file says one thing and the store another; the store is what the caller gets.
        var auth = Bootstrap("legacy-role", Permissions.RunsRead);
        var stored = new StoredMappingStub(Mapping("auditor", Permissions.ConfigRead));

        var identity = ResolverUnderTest.Over(auth, stored)
            .Resolve(ResolverUnderTest.Caller(auth, ("roles", "auditor"), ("roles", "legacy-role")));

        identity.Roles.Should().Contain("auditor");
        identity.Permissions.Should().Contain(Permissions.ConfigRead);
        identity.Permissions.Should().NotContain(Permissions.RunsControl);
        identity.Permissions.Should().NotContain(
            Permissions.RunsDelete, "the file's bundle no longer decides anything");
    }

    [Fact]
    public void Store_HoldsNoMapping_TheFileSeedsIt()
    {
        // The migration has not spoken, so the mapping the installation booted with governs.
        var auth = Bootstrap("release-manager", Permissions.RunsControl);

        var identity = ResolverUnderTest.With(auth)
            .Resolve(ResolverUnderTest.Caller(auth, ("roles", "release-manager")));

        identity.Roles.Should().Equal("release-manager");
        identity.Permissions.Should().Contain(Permissions.RunsControl);
    }

    [Fact]
    public void Store_ReadFails_TheFileStillSeedsIt()
    {
        // A store that cannot answer must not strip every caller of the roles they had.
        var auth = Bootstrap("release-manager", Permissions.RunsControl);
        var stored = new StoredMappingStub(null);

        var identity = ResolverUnderTest.Over(auth, stored)
            .Resolve(ResolverUnderTest.Caller(auth, ("roles", "release-manager")));

        identity.Permissions.Should().Contain(Permissions.RunsControl);
    }

    [Fact]
    public void Mapping_ClaimNamesComeFromTheStore_NotTheBootstrapBlock()
    {
        var auth = new TokenAuthorityConfig { RoleClaim = "roles", GroupClaim = "groups" };
        var stored = new StoredMappingStub(new RoleMappingConfig
        {
            RoleClaim = "app_roles",
            GroupRoles = new Dictionary<string, List<string>> { ["platform"] = [BuiltInRoles.Reader] },
            GroupClaim = "memberships",
        });

        var identity = ResolverUnderTest.Over(auth, stored).Resolve(
            ResolverUnderTest.Caller(auth, ("app_roles", "auditor"), ("memberships", "platform")));

        identity.RoleClaim.Should().Be("app_roles");
        identity.GroupClaim.Should().Be("memberships");
        identity.RoleClaimValues.Should().Equal("auditor");
        identity.Roles.Should().Contain(BuiltInRoles.Reader);
    }

    [Fact]
    public void Mapping_CustomRoleNamesABuiltIn_IsReportedAndIgnoredAsBefore()
    {
        var auth = new TokenAuthorityConfig();
        var stored = new StoredMappingStub(Mapping(BuiltInRoles.Admin, Permissions.RunsRead));

        var identity = ResolverUnderTest.Over(auth, stored)
            .Resolve(ResolverUnderTest.Caller(auth, ("roles", BuiltInRoles.Admin)));

        identity.Permissions.Should().Contain(Permissions.SecretsWrite,
            "a built-in bundle is never replaced by a name that collides with it");
        identity.Findings.Should().ContainSingle().Which.Should().Contain(BuiltInRoles.Admin);
    }

    [Fact]
    public void Mapping_CustomRoleNamesAnUnknownPermission_IsReportedAndDropped()
    {
        var auth = new TokenAuthorityConfig();
        var stored = new StoredMappingStub(Mapping("half-right", Permissions.ConfigRead, "config.approve"));

        var identity = ResolverUnderTest.Over(auth, stored)
            .Resolve(ResolverUnderTest.Caller(auth, ("roles", "half-right")));

        identity.Permissions.Should().Contain(Permissions.ConfigRead);
        identity.Permissions.Should().NotContain("config.approve");
        identity.Findings.Should().ContainSingle()
            .Which.Should().Contain("config.approve").And.Contain("half-right");
    }

    [Fact]
    public void AdminGrant_StaysAnEnvironmentVariable()
    {
        // The way back in is not reached through the surface it rescues: the grant is read
        // from the environment and from nothing else, whatever the stored mapping says.
        var asked = new List<string>();
        var auth = new TokenAuthorityConfig();
        var stored = new StoredMappingStub(new RoleMappingConfig());
        var source = new RoleMappingSource(stored, auth);
        source.AdoptStore();
        var resolver = ResolverUnderTest.Resolver(
            source, ResolverUnderTest.Grant("sub:locked-out", asked));

        var identity = resolver.Resolve(ResolverUnderTest.Caller(auth, ("sub", "locked-out")));

        identity.Roles.Should().Contain(BuiltInRoles.Admin);
        asked.Should().Equal(AdminGrant.EnvVar);
    }

    private static TokenAuthorityConfig Bootstrap(string role, params string[] permissions) =>
        new() { Roles = new Dictionary<string, List<string>> { [role] = [.. permissions] } };

    private static RoleMappingConfig Mapping(string role, params string[] permissions) =>
        new() { Roles = new Dictionary<string, List<string>> { [role] = [.. permissions] } };
}
