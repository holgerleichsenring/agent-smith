using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks.Trust;

public sealed class GitLabMemberAccessTrustTests
{
    private const string RepoUrl = "https://gitlab.example.com/org/r";
    private static readonly PrCommentAuthor Author = new(RepoUrl, "7", "42", "dev");

    private readonly Mock<IGitLabMemberAccessReader> _members = new();

    private GitLabMemberAccessTrust CreateSut() =>
        new(PrCommentHandlerFixture.Repos(RepoUrl), _members.Object,
            PrCommentHandlerFixture.NewCache(), NullLogger<GitLabMemberAccessTrust>.Instance);

    [Theory]
    [InlineData(10, false)]
    [InlineData(20, false)]
    [InlineData(30, true)]
    [InlineData(40, true)]
    [InlineData(50, true)]
    public async Task IsTrustedAsync_DeveloperOrAbove(int accessLevel, bool trusted)
    {
        _members.Setup(m => m.ReadAccessLevelAsync(It.Is<RepoConnection>(r => r.Url == RepoUrl), "7", "42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessLevel);

        (await CreateSut().IsTrustedAsync(Author, default)).Should().Be(trusted);
    }

    [Fact]
    public async Task IsTrustedAsync_Twice_AsksGitLabOnce()
    {
        _members.Setup(m => m.ReadAccessLevelAsync(It.Is<RepoConnection>(r => r.Url == RepoUrl), "7", "42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(30);
        var sut = CreateSut();

        await sut.IsTrustedAsync(Author, default);
        await sut.IsTrustedAsync(Author, default);

        _members.Verify(m => m.ReadAccessLevelAsync(It.Is<RepoConnection>(r => r.Url == RepoUrl), "7", "42", It.IsAny<CancellationToken>()), Times.Once);
    }

    // 2026-10-02-5f89a: a GitLab project id is unique per instance only.
    [Fact]
    public async Task GitLabMemberAccessTrust_SameRepoIdOnTwoHosts_KeepsTwoVerdicts()
    {
        const string otherUrl = "https://gitlab.other.example/org/r";
        var lookup = new Mock<IPrCommentRepoLookup>();
        foreach (var url in new[] { RepoUrl, otherUrl })
            lookup.Setup(l => l.Find(url)).Returns(new ConfiguredRepo(
                "p", new ResolvedProject(), new RepoConnection { Name = "r", Url = url }));
        _members.Setup(m => m.ReadAccessLevelAsync(
                It.Is<RepoConnection>(r => r.Url == RepoUrl), "7", "42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(30);
        _members.Setup(m => m.ReadAccessLevelAsync(
                It.Is<RepoConnection>(r => r.Url == otherUrl), "7", "42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);
        var sut = new GitLabMemberAccessTrust(lookup.Object, _members.Object,
            PrCommentHandlerFixture.NewCache(), NullLogger<GitLabMemberAccessTrust>.Instance);

        var first = await sut.IsTrustedAsync(Author, default);
        var second = await sut.IsTrustedAsync(Author with { RepositoryUrl = otherUrl }, default);

        first.Should().BeTrue();
        second.Should().BeFalse("the verdict of one instance is not the other's");
    }
}
