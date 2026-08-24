using System.Reflection;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Security;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0517: the acceptance criterion, pinned before anything else. The enforce default stays
/// off until the dashboard has a way to sign in, and until then a hub invocation must cost
/// exactly what it cost before this phase — no token, no refusal, on every method in the
/// table.
/// <para>
/// The assertion is that no completion names a PERMISSION, not that no completion carries
/// an error: this boot deliberately has no Redis, so several of these methods fail on their
/// own. The handler failing is the hub working; the filter speaking would be the defect.
/// </para>
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class HubEnforceSwitchOffTests(PermissiveAuthorityFixture fixture)
    : IClassFixture<PermissiveAuthorityFixture>
{
    [Fact]
    public async Task Enforce_SwitchOff_HubMethodsBehaveAsBefore()
    {
        var refused = new List<string>();
        foreach (var method in HubMethods())
        {
            var completion = await new HubLongPoll(fixture.Server.Client, "/hub/jobs", token: null)
                .InvokeAsync(method.Name, Arguments(method));
            refused.AddRange(Permissions.All
                .Where(permission => completion.Contains(permission, StringComparison.Ordinal))
                .Select(permission => $"{method.Name} -> {permission}"));
        }

        refused.Should().BeEmpty(
            "the filter reads the enforce switch, and with it off it decides nothing\n  "
            + string.Join("\n  ", refused));
    }

    private static IEnumerable<MethodInfo> HubMethods() =>
        HubMethodPermissions.MethodNames.Select(name => typeof(JobsHub).GetMethod(name)!);

    // Every argument any of these takes is a run id, a repository name, a stream cursor or
    // a page size. None is read before the filter has already decided.
    private static object?[] Arguments(MethodInfo method) =>
        [.. method.GetParameters().Select(p =>
            p.ParameterType == typeof(string) ? "p0517-not-a-real-run" : (object?)null)];
}
