using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0176a: AsyncLocal CallScope round-trips and nests cleanly so the
/// EventPublishing decorator reads the current handler's attribution
/// without ctor plumbing.
/// </summary>
public sealed class CallScopeAccessorTests
{
    [Fact]
    public void BeginCallScope_NoActiveScope_CurrentCallScopeIsNull()
    {
        var accessor = new AsyncLocalRunContextAccessor();
        accessor.CurrentCallScope.Should().BeNull();
    }

    [Fact]
    public void BeginCallScope_OpensScope_CurrentCallScopeReflectsRolePhaseRepo()
    {
        var accessor = new AsyncLocalRunContextAccessor();
        using var _ = accessor.BeginCallScope("planner", "Plan", "repo-a");
        accessor.CurrentCallScope.Should().NotBeNull();
        accessor.CurrentCallScope!.Role.Should().Be("planner");
        accessor.CurrentCallScope.Phase.Should().Be("Plan");
        accessor.CurrentCallScope.RepoName.Should().Be("repo-a");
    }

    [Fact]
    public void BeginCallScope_RestoresPreviousScopeOnDispose()
    {
        var accessor = new AsyncLocalRunContextAccessor();
        accessor.CurrentCallScope.Should().BeNull();
        using (accessor.BeginCallScope("outer", "Plan"))
        {
            accessor.CurrentCallScope!.Role.Should().Be("outer");
        }
        accessor.CurrentCallScope.Should().BeNull();
    }

    [Fact]
    public void BeginCallScope_NestedScopes_TopmostScopeWins()
    {
        var accessor = new AsyncLocalRunContextAccessor();
        using var outer = accessor.BeginCallScope("outer", "Plan", "repo-a");
        accessor.CurrentCallScope!.Role.Should().Be("outer");
        using (accessor.BeginCallScope("inner", "Implementation", "repo-b"))
        {
            accessor.CurrentCallScope!.Role.Should().Be("inner");
            accessor.CurrentCallScope.RepoName.Should().Be("repo-b");
        }
        accessor.CurrentCallScope!.Role.Should().Be("outer");
        accessor.CurrentCallScope.RepoName.Should().Be("repo-a");
    }

    [Fact]
    public void BeginCallScope_OmittedRepoName_ScopeRepoNameIsNull()
    {
        var accessor = new AsyncLocalRunContextAccessor();
        using var _ = accessor.BeginCallScope("planner", "Plan");
        accessor.CurrentCallScope!.RepoName.Should().BeNull();
    }
}
