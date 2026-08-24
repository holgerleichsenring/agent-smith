using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503d: an installation whose org chart does not fit the three shipped roles re-bundles
/// the catalog. Additive in both directions — a custom role never replaces a built-in one,
/// and it cannot invent a permission: a name the closed catalog does not contain is
/// filtered out of the effective set FIRST and reported second, because "additive cannot
/// grant what the catalog lacks" without a filter is a claim with no mechanism.
/// </summary>
public sealed class CustomRoleTests
{
    [Fact]
    public void Roles_CustomRoleGrantsItsSubset_AndNothingMore()
    {
        var auth = Custom("config-viewer", Permissions.ConfigRead);

        var identity = ResolverUnderTest.With(auth)
            .Resolve(ResolverUnderTest.Caller(auth, ("roles", "config-viewer")));

        identity.Roles.Should().Equal("config-viewer");
        identity.Permissions.Should().Contain(Permissions.ConfigRead);
        identity.Permissions.Should().NotContain(Permissions.SecretsRead);
        identity.Permissions.Should().NotContain(Permissions.ConfigWrite);
    }

    [Fact]
    public void Roles_CustomRoleNamingAnUnknownPermission_DoesNotGrantItAndRecordsAFinding()
    {
        var auth = Custom("half-right", Permissions.ConfigRead, "config.approve");

        var identity = ResolverUnderTest.With(auth)
            .Resolve(ResolverUnderTest.Caller(auth, ("roles", "half-right")));

        identity.Permissions.Should().Contain(Permissions.ConfigRead);
        identity.Permissions.Should().NotContain("config.approve");
        identity.Findings.Should().ContainSingle()
            .Which.Should().Contain("config.approve").And.Contain("half-right");
    }

    [Fact]
    public void Roles_CustomRoleNamedAdmin_DoesNotOverrideTheBuiltIn()
    {
        var auth = Custom(BuiltInRoles.Admin, Permissions.RunsRead);

        var identity = ResolverUnderTest.With(auth)
            .Resolve(ResolverUnderTest.Caller(auth, ("roles", BuiltInRoles.Admin)));

        identity.Permissions.Should().Contain(Permissions.SecretsWrite,
            "a built-in bundle is never replaced by a name that collides with it");
        identity.Findings.Should().ContainSingle().Which.Should().Contain(BuiltInRoles.Admin);
    }

    private static TokenAuthorityConfig Custom(string name, params string[] permissions) =>
        new() { Roles = new Dictionary<string, List<string>> { [name] = [.. permissions] } };
}
