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
        _members.Setup(m => m.ReadAccessLevelAsync(RepoUrl, "7", "42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessLevel);

        (await CreateSut().IsTrustedAsync(Author, default)).Should().Be(trusted);
    }

    [Fact]
    public async Task IsTrustedAsync_Twice_AsksGitLabOnce()
    {
        _members.Setup(m => m.ReadAccessLevelAsync(RepoUrl, "7", "42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(30);
        var sut = CreateSut();

        await sut.IsTrustedAsync(Author, default);
        await sut.IsTrustedAsync(Author, default);

        _members.Verify(m => m.ReadAccessLevelAsync(RepoUrl, "7", "42", It.IsAny<CancellationToken>()), Times.Once);
    }
}
