using AgentSmith.Application.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

public sealed class ToolchainGroupOrderTests
{
    [Fact]
    public void ToolchainGroupOrder_RootWorkdirGroup_IsFirst()
    {
        var groups = Group(("node", "src/dashboard"), ("dotnet", "."), ("python", "tools"));

        ToolchainGroupOrder.RootFirst(groups, m => m.Workdir).Select(g => g.Key).Should().Equal("dotnet", "node", "python");
    }

    [Fact]
    public void ToolchainGroupOrder_NoRootContext_KeepsListingOrder()
    {
        var groups = Group(("node", "web"), ("dotnet", "src/Api"), ("python", "tools"));

        ToolchainGroupOrder.RootFirst(groups, m => m.Workdir).Select(g => g.Key).Should().Equal("node", "dotnet", "python");
    }

    private static IEnumerable<IGrouping<string, (string Image, string Workdir)>> Group(
        params (string Image, string Workdir)[] members) =>
        members.GroupBy(m => m.Image, StringComparer.Ordinal);
}
