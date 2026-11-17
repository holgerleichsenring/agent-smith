using AgentSmith.Contracts.Providers;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>2026-11-17-713eb: only a branch under agent-smith/ is the framework's to delete.</summary>
public sealed class WorkBranchesTests
{
    [Theory]
    [InlineData("agent-smith/42", true)]
    [InlineData("agent-smith/init", true)]
    [InlineData("refs/heads/agent-smith/42", true)]
    [InlineData("agent-smith/", false)]
    [InlineData("agent-smithy/x", false)]
    [InlineData("feature/x", false)]
    [InlineData(null, false)]
    public void WorkBranches_Owns_OnlyBranchesUnderThePrefix(string? branch, bool owned) =>
        WorkBranches.Owns(branch).Should().Be(owned);
}
