using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Resolvers;

/// <summary>
/// 2026-10-02-5f89g: a clone, fetch or push authenticates with its repo's own auth secret — the
/// type-keyed GITHUB_TOKEN / GITLAB_TOKEN / AZURE_DEVOPS_TOKEN lookup is gone.
/// </summary>
public sealed class GitTokenResolverTests
{
    private readonly GitTokenResolver _sut = new(
        TestCredentials.With(("gitlab_one", "token-one"), ("gitlab_two", "token-two")));

    [Fact]
    public void GitTokenResolver_RepoOfTheSecondGitLabConnection_GetsItsOwnToken()
    {
        var first = new RepoConnection { Name = "a", Type = RepoType.GitLab, Url = "https://one.example/g/a", Auth = "gitlab_one" };
        var second = first with { Name = "b", Url = "https://two.example/g/b", Auth = "gitlab_two" };

        _sut.For(first).Token.Should().Be("token-one");
        _sut.For(second).Token.Should().Be("token-two");
    }

    [Fact]
    public void GitTokenResolver_LocalRepoWithStubUrl_GetsNoCredentialAndNoError()
    {
        var local = new RepoConnection { Name = "l", Type = RepoType.Local, Path = ".", Url = "https://stub.test/l" };

        _sut.For(local).Should().Be(GitCredential.None);
    }

    [Fact]
    public void For_RemoteRepoWithMissingSecret_ThrowsNamingTheSecret()
    {
        var repo = new RepoConnection { Name = "r", Type = RepoType.GitHub, Url = "https://github.com/o/r", Auth = "nope" };

        _sut.Invoking(s => s.For(repo)).Should().Throw<MissingCredentialException>().WithMessage("*'nope'*");
    }
}
