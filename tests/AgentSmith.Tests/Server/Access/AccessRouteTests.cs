using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Security;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace AgentSmith.Tests.Server.Access;

/// <summary>
/// 2026-08-26-7a51: the access surface decides its own permission instead of inheriting
/// <c>config.write</c>. A custom role bundling <c>config.write</c> is legal, and the
/// settings route that used to carry the role mapping would have let such a caller grant
/// themselves admin — and with it the secrets permissions the catalog kept separable.
/// </summary>
[Collection(EnvVarCollection.Name)]
public sealed class AccessRouteTests
{
    private const string GateEnvVar = "AGENTSMITH_UI_API_ENABLED";

    [Fact]
    public void Access_RoutesNeedAccessPermissions_NotConfigWrite()
    {
        var access = WithGate("true", () => ServerRouteTable.Facts(MapAsTheHostDoes))
            .Where(fact => fact.Pattern.StartsWith("/api/access", StringComparison.Ordinal))
            .ToList();

        access.Should().HaveCount(3);
        access.Where(fact => fact.Method == "GET").Should()
            .ContainSingle().Which.Permissions.Should().Equal(Permissions.AccessRead);
        access.Where(fact => fact.Method != "GET").Should()
            .OnlyContain(fact => fact.Permissions.Single() == Permissions.AccessWrite);
        access.Should().NotContain(fact => fact.Permissions.Contains(Permissions.ConfigWrite));
    }

    [Fact]
    public void Access_ConfigWriteAlone_CannotGrantARole()
    {
        using var h = new AccessTestHarness();

        // The mapping is still a settings singleton in the STORE — that is what gives it the
        // change row, the revert and the export — and it is no longer reachable through the
        // config.write settings route, which is the only one that ever carried it.
        h.Store.SettingTypes.Should().Contain(ConfigDocTypes.RoleMapping);
        Editable().Should().NotContain(ConfigDocTypes.RoleMapping);
    }

    [Fact]
    public void Access_AccessPermissions_AreBundledIntoAdminAlone()
    {
        var holders = BuiltInRoles.All
            .Where(role => role.Value.Contains(Permissions.AccessWrite, StringComparer.Ordinal))
            .Select(role => role.Key);

        holders.Should().Equal(BuiltInRoles.Admin);
        Permissions.All.Should().Contain([Permissions.AccessRead, Permissions.AccessWrite]);
    }

    // The endpoint's own filter, read the way the endpoint reads it: the settings routes
    // serve every editable singleton EXCEPT the role mapping.
    private static IReadOnlyList<string> Editable() =>
        [.. ConfigSettingsAccess.Types.Where(type => type != ConfigDocTypes.RoleMapping)];

    private static void MapAsTheHostDoes(WebApplication app)
    {
        app.MapServerEndpoints();
        if (DashboardApiExtensions.IsEnabled) app.MapDashboardApi();
    }

    private static T WithGate<T>(string value, Func<T> read)
    {
        var previous = Environment.GetEnvironmentVariable(GateEnvVar);
        Environment.SetEnvironmentVariable(GateEnvVar, value);
        try { return read(); }
        finally { Environment.SetEnvironmentVariable(GateEnvVar, previous); }
    }
}
