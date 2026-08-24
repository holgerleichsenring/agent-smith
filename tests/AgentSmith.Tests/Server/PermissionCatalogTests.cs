using AgentSmith.Server.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0503a: the catalog and the two metadata types, judged on their own — the route table
/// is <see cref="RoutePermissionGuardTests"/>' subject.
/// <para>
/// The load-bearing claim here is a NEGATIVE one: the permission declaration must not be
/// something the routing middleware inspects. <c>RequireAuthorization</c> attaches
/// <c>IAuthorizeData</c>, and EndpointMiddleware throws on an executed endpoint carrying
/// it when no authorization middleware ran — which this server has none of.
/// </para>
/// </summary>
public sealed class PermissionCatalogTests
{
    [Fact]
    public void Permissions_EveryPermission_BelongsToAtLeastOneRole()
    {
        var bundled = BuiltInRoles.All.Values.SelectMany(names => names).ToHashSet(StringComparer.Ordinal);

        Permissions.All.Should().OnlyContain(
            permission => bundled.Contains(permission),
            "a permission no shipped role grants is a capability nobody can be given");
    }

    // reader sees what the agent DID. The configuration is where the credentials, the
    // trackers and the repositories are named, and that is a different question.
    [Fact]
    public void Permissions_ReaderHoldsNoConfigRead()
    {
        BuiltInRoles.All[BuiltInRoles.Reader].Should().NotContain(Permissions.ConfigRead);
        BuiltInRoles.All[BuiltInRoles.Reader].Should().NotContain(Permissions.SecretsRead);
        BuiltInRoles.All[BuiltInRoles.Reader].Should().Contain(Permissions.RunsRead);
    }

    // The permissions on a route are required TOGETHER — that is the whole reason the
    // four routes crossing into secrets can state both.
    [Fact]
    public void Permissions_ConfigWriteAlone_SatisfiesNoSecretsRoute()
    {
        var held = new HashSet<string>(StringComparer.Ordinal) { Permissions.ConfigWrite };
        var revert = new RequiresPermission(Permissions.ConfigWrite, Permissions.SecretsWrite);

        revert.Names.All(held.Contains).Should().BeFalse(
            "config.write alone does not satisfy a route that also states secrets.write");
    }

    [Fact]
    public void Metadata_RequiresPermission_IsNotAuthorizeData()
    {
        var declaration = new RequiresPermission(Permissions.ConfigWrite);

        declaration.Should().NotBeAssignableTo<IAuthorizeData>(
            "IAuthorizeData on an endpoint with no authorization middleware is a 500");
        declaration.Should().NotBeAssignableTo<AuthorizationPolicy>();
        declaration.Names.Should().Equal(Permissions.ConfigWrite);
    }

    [Fact]
    public void Metadata_RequiresPermission_RefusesAnEmptyList()
    {
        var act = () => new RequiresPermission();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Metadata_AnonymousDeclaration_CarriesAReason()
    {
        var declaration = new AnonymousRoute("a liveness probe cannot authenticate");

        declaration.Reason.Should().NotBeNullOrWhiteSpace();
        // AllowAnonymousAttribute is what a route pairs it with, and it is inert for the
        // same reason: IAllowAnonymous only, never IAuthorizeData.
        new AllowAnonymousAttribute().Should().NotBeAssignableTo<IAuthorizeData>();
    }
}
