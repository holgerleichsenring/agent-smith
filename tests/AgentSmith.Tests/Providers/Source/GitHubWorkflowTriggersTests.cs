using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>2026-11-24-f9c5d: a workflow's <c>on:</c> read for pull requests by base branch, and for workflow_dispatch.</summary>
public sealed class GitHubWorkflowTriggersTests
{
    [Theory]
    [InlineData("on: pull_request", "agent-smith/series/sr-1", true)]
    [InlineData("on: [push, pull_request]", "main", true)]
    [InlineData("on: push", "main", false)]
    [InlineData("on:\n  pull_request:\n    branches: [main]", "agent-smith/series/sr-1", false)]
    [InlineData("on:\n  pull_request:\n    branches: [main, 'agent-smith/**']", "agent-smith/series/sr-1", true)]
    [InlineData("on:\n  pull_request:\n    branches: ['agent-smith/*']", "agent-smith/series/sr-1", false)]
    [InlineData("on:\n  pull_request_target:\n    branches-ignore: ['agent-smith/**']", "agent-smith/series/sr-1", false)]
    [InlineData("on:\n  pull_request:\n    branches: ['**', '!agent-smith/**']", "agent-smith/series/sr-1", false)]
    public void RunsFor_ReadsTheFilterAsGitHubDoes(string on, string baseBranch, bool runs) =>
        GitHubWorkflowTriggers.Parse($"name: ci\n{on}\njobs: {{}}\n").RunsFor(baseBranch).Should().Be(runs);

    [Fact]
    public void Parse_WorkflowDispatch_IsDeclared_AndBrokenYamlIsNoTrigger()
    {
        GitHubWorkflowTriggers.Parse("on:\n  pull_request:\n  workflow_dispatch:\n").Dispatch.Should().BeTrue();
        GitHubWorkflowTriggers.Parse("on: pull_request").Dispatch.Should().BeFalse();
        GitHubWorkflowTriggers.Parse("on: [unclosed").Should().Be(GitHubWorkflowTriggers.None);
    }
}
