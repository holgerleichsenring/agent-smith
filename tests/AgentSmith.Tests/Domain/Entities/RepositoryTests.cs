using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Domain.Entities;

public sealed class RepositoryTests
{
    [Fact]
    public void WorkPathFor_RepoName_IsUnderTheWorkspaceRoot()
    {
        Repository.WorkPathFor("api").Should().Be("/work/api");
        Repository.WorkspaceRoot.Should().Be("/work");
    }

    [Fact]
    public void Constructor_StoresBranchAndRemoteUrl()
    {
        var repo = new Repository(new BranchName("feature/foo"), "git@example.com:org/repo.git");

        repo.CurrentBranch.Value.Should().Be("feature/foo");
        repo.RemoteUrl.Should().Be("git@example.com:org/repo.git");
    }
}
