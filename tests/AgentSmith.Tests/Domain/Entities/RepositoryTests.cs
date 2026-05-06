using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Domain.Entities;

public sealed class RepositoryTests
{
    [Fact]
    public void LocalPath_AlwaysReturnsWorkConstant()
    {
        var repo = new Repository(new BranchName("main"), "https://example.com/repo.git");

        repo.LocalPath.Should().Be("/work");
        repo.LocalPath.Should().Be(Repository.SandboxWorkPath);
    }

    [Fact]
    public void Constructor_StoresBranchAndRemoteUrl()
    {
        var repo = new Repository(new BranchName("feature/foo"), "git@example.com:org/repo.git");

        repo.CurrentBranch.Value.Should().Be("feature/foo");
        repo.RemoteUrl.Should().Be("git@example.com:org/repo.git");
    }
}
