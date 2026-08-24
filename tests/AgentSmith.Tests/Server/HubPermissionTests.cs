using System.Reflection;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0503c: every hub method names the permission it needs, and a twelfth method fails
/// the build rather than arriving unclassified.
/// <para>
/// The second load-bearing claim is a NEGATIVE one, and it is why the declaration is a
/// table at all: SignalR's dispatcher evaluates a hub method's own AuthorizeAttributes
/// through IAuthorizationService on every invocation, outside the middleware pipeline —
/// so an IAuthorizeData attribute on a method would refuse or throw on this server,
/// which registers no authorization services.
/// </para>
/// </summary>
public sealed class HubPermissionTests
{
    private static IReadOnlyList<MethodInfo> PublicHubMethods() =>
        [.. typeof(JobsHub)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)];

    [Fact]
    public void HubPermissions_EveryPublicHubMethod_IsNamedInTheTable()
    {
        var named = HubMethodPermissions.MethodNames.ToHashSet(StringComparer.Ordinal);

        var unnamed = PublicHubMethods().Select(m => m.Name).Where(n => !named.Contains(n)).ToList();

        unnamed.Should().BeEmpty(
            "a hub method no table entry names is a method nobody can authorize. Add it "
            + "to HubMethodPermissions.\n  " + string.Join("\n  ", unnamed));
        named.Should().OnlyContain(
            name => PublicHubMethods().Any(m => m.Name == name),
            "an entry for a method that no longer exists authorizes nothing");
    }

    [Fact]
    public void HubPermissions_TheTableNamesOnlyCataloguedPermissions()
    {
        var catalogued = Permissions.All.ToHashSet(StringComparer.Ordinal);

        var stated = HubMethodPermissions.MethodNames
            .SelectMany(name => HubMethodPermissions.For(name)!.Names);

        stated.Should().OnlyContain(
            permission => catalogued.Contains(permission),
            "a permission outside the catalog is one no role can bundle");
    }

    // The expansion refcount is process-global, so this pair is a cross-viewer mutation
    // rather than a read — separable from runs.read on purpose.
    [Fact]
    public void HubPermissions_TheSandboxPair_TakesTheWatchPermission()
    {
        HubMethodPermissions.For(nameof(JobsHub.ExpandSandbox))!.Names
            .Should().Equal(Permissions.RunsWatch);
        HubMethodPermissions.For(nameof(JobsHub.CollapseSandbox))!.Names
            .Should().Equal(Permissions.RunsWatch);
        HubMethodPermissions.For(nameof(JobsHub.SubscribeRun))!.Names
            .Should().NotContain(Permissions.RunsWatch);
    }

    // A reader who loses the live drawer loses most of what watching a run is for.
    [Fact]
    public void HubPermissions_TheWatchPermission_IsHeldByTheReaderBundle()
    {
        BuiltInRoles.All[BuiltInRoles.Reader].Should().Contain(Permissions.RunsWatch);
        BuiltInRoles.All[BuiltInRoles.Operator].Should().Contain(Permissions.RunsWatch);
    }

    [Fact]
    public void HubMethods_CarryNoAuthorizeData_SoTheDispatcherEnforcesNothing()
    {
        var annotated = PublicHubMethods()
            .Where(m => m.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>().Any())
            .Select(m => m.Name)
            .ToList();

        annotated.Should().BeEmpty(
            "SignalR's dispatcher evaluates a method's own IAuthorizeData through "
            + "IAuthorizationService, which this server does not register.\n  "
            + string.Join("\n  ", annotated));
        typeof(JobsHub).GetCustomAttributes(inherit: true).OfType<IAuthorizeData>()
            .Should().BeEmpty("the same is true of the hub class itself");
    }

    [Fact]
    public void HubPermissions_AnUnknownMethod_ResolvesToNothing()
    {
        HubMethodPermissions.For("MethodThatDoesNotExist").Should().BeNull(
            "the caller decides what an unclassified method costs, and it refuses");
    }
}
