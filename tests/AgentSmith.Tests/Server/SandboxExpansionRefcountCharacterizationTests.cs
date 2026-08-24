using System.Reflection;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0503c: CHARACTERIZATION, not a specification — these tests pin what the expansion
/// refcount does today so a successor phase has a red starting point, and they are green
/// on behaviour that is wrong.
/// <para>
/// The registry is a process-global singleton over a per-(run, repo) counter, and
/// RunEventRouter gates sandbox fan-out on it. Nothing ties a count to the connection
/// that made it, so one viewer's collapse turns another viewer's live drawer off, and a
/// viewer that simply goes away never decrements at all. Authorization does not repair a
/// counter; ownership does.
/// </para>
/// </summary>
public sealed class SandboxExpansionRefcountCharacterizationTests
{
    private const string Run = "run-1";
    private const string Repo = "repo-a";

    [Fact]
    public void ExpansionRegistry_CollapseFromASecondCaller_SilencesTheFirstCallersDrawer()
    {
        var registry = new SandboxExpansionRegistry();

        registry.Expand(Run, Repo);          // viewer A opens the drawer
        registry.Collapse(Run, Repo);        // viewer B closes one it never opened

        registry.IsExpanded(Run, Repo).Should().BeFalse(
            "CHARACTERIZATION: the counter is shared, so B's collapse ends A's fan-out. "
            + "A drawer that closes with its own connection needs per-connection ownership.");
    }

    [Fact]
    public void ExpansionRegistry_ConnectionGoesAwayWithoutCollapsing_StaysExpandedForever()
    {
        var registry = new SandboxExpansionRegistry();

        registry.Expand(Run, Repo);          // the tab is closed here; nothing else happens

        registry.IsExpanded(Run, Repo).Should().BeTrue(
            "CHARACTERIZATION: fan-out stays on for the life of the process");
        typeof(JobsHub)
            .GetMethod("OnDisconnectedAsync", BindingFlags.Public | BindingFlags.Instance
                | BindingFlags.DeclaredOnly)
            .Should().BeNull(
                "and this is why: the hub never learns that the connection went away, so "
                + "there is no moment at which the decrement could happen");
    }
}
