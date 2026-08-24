using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503d: the directory says which roles, and the comparisons are pinned PER SURFACE.
/// Role names fold case — a directory decides the capitalisation and an operator cannot.
/// Group values do not: an Entra group value is an opaque object identifier, and
/// case-folding an opaque identifier is a smell. A Keycloak group path's leading slash is
/// the one character normalised away, because the console does not show it.
/// </summary>
public sealed class RoleResolutionTests
{
    private const string MappedGroup = "aa11bbcc-dd22-4ee3-8ff4-abcdefabcdef";

    [Fact]
    public void Roles_RoleClaimCarriesRoleNames_ResolvesWithoutAMapping()
    {
        var auth = new TokenAuthorityConfig();

        var identity = ResolverUnderTest.With(auth)
            .Resolve(ResolverUnderTest.Caller(auth, ("roles", BuiltInRoles.Operator)));

        identity.Roles.Should().Equal(BuiltInRoles.Operator);
        identity.Permissions.Should().Contain(Permissions.RunsControl);
        identity.Permissions.Should().NotContain(Permissions.ConfigWrite);
    }

    [Fact]
    public void Roles_ConfiguredClaimName_ReadsOnlyThatClaimWhenBothArePresent()
    {
        var auth = new TokenAuthorityConfig { RoleClaim = "app_roles" };

        var identity = ResolverUnderTest.With(auth).Resolve(ResolverUnderTest.Caller(
            auth, ("app_roles", BuiltInRoles.Reader), ("roles", BuiltInRoles.Admin)));

        identity.Roles.Should().ContainSingle("the claim name is configuration, so a claim "
            + "nobody configured grants nothing").Which.Should().Be(BuiltInRoles.Reader);
    }

    [Fact]
    public void Roles_RoleNameCasingDiffers_StillResolves()
    {
        var auth = new TokenAuthorityConfig();

        var identity = ResolverUnderTest.With(auth)
            .Resolve(ResolverUnderTest.Caller(auth, ("roles", "Admin")));

        identity.Permissions.Should().Contain(Permissions.SecretsWrite,
            "BuiltInRoles.All is an ordinal dictionary, so 'Admin' resolved to nothing "
            + "until the catalog was rebuilt case-insensitively");
    }

    [Fact]
    public void Roles_GroupValueCasingDiffers_DoesNotResolve()
    {
        var auth = Mapping((MappedGroup, [BuiltInRoles.Operator]));

        var identity = ResolverUnderTest.With(auth)
            .Resolve(ResolverUnderTest.Caller(auth, ("groups", MappedGroup.ToUpperInvariant())));

        identity.Roles.Should().BeEmpty("an opaque identifier is compared ordinally");
        identity.GroupClaimValues.Should().ContainSingle("and it is still shown, unmapped");
    }

    [Fact]
    public void Roles_GroupPathWithLeadingSlash_MatchesTheKeyWithout()
    {
        var auth = Mapping(("platform-operators", [BuiltInRoles.Operator]));

        var identity = ResolverUnderTest.With(auth)
            .Resolve(ResolverUnderTest.Caller(auth, ("groups", "/platform-operators")));

        identity.Roles.Should().Equal(BuiltInRoles.Operator);
    }

    [Fact]
    public void Roles_TwoMappedGroups_UnionTheirPermissions()
    {
        var auth = Mapping(("readers", [BuiltInRoles.Reader]), ("operators", [BuiltInRoles.Operator]));

        var identity = ResolverUnderTest.With(auth).Resolve(ResolverUnderTest.Caller(
            auth, ("groups", "readers"), ("groups", "operators")));

        identity.Roles.Should().BeEquivalentTo([BuiltInRoles.Reader, BuiltInRoles.Operator]);
        identity.Permissions.Should().Contain([Permissions.RunsRead, Permissions.RunsControl]);
    }

    [Fact]
    public void Roles_MappedAndUnmappedGroupTogether_ResolvesExactlyOneRole()
    {
        var auth = Mapping(("readers", [BuiltInRoles.Reader]));

        var identity = ResolverUnderTest.With(auth).Resolve(ResolverUnderTest.Caller(
            auth, ("groups", "readers"), ("groups", "a-group-nobody-mapped")));

        identity.Roles.Should().Equal(BuiltInRoles.Reader);
        identity.GroupClaimValues.Should().HaveCount(2, "both are reported, one is mapped");
    }

    private static TokenAuthorityConfig Mapping(params (string Group, List<string> Roles)[] mapping) =>
        new() { GroupRoles = mapping.ToDictionary(m => m.Group, m => m.Roles) };
}
