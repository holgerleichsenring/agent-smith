using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks.Trust;

/// <summary>2026-10-25-9dbbe: GitHub is asked for the author's permission on the configured repository.</summary>
public sealed class GitHubCollaboratorPermissionTrustTests
{
    private const string RepoUrl = "https://github.com/o/r";
    private static readonly PrCommentAuthor Author = new(RepoUrl, "o/r", "dev", "dev");

    private readonly Mock<IGitHubCollaboratorPermissionReader> _permissions = new();

    private GitHubCollaboratorPermissionTrust CreateSut(string? configuredUrl = RepoUrl) =>
        new(PrCommentHandlerFixture.Repos(configuredUrl), _permissions.Object,
            PrCommentHandlerFixture.NewCache(), NullLogger<GitHubCollaboratorPermissionTrust>.Instance);

    private void Answer(params string[] roles) =>
        _permissions.Setup(p => p.ReadAsync(It.Is<RepoConnection>(r => r.Url == RepoUrl), "dev", It.IsAny<CancellationToken>()))
            .ReturnsAsync(roles);

    [Theory]
    [InlineData("write")]
    [InlineData("maintain")]
    [InlineData("admin")]
    public async Task GitHubTrust_WritePermission_Trusted(string role)
    {
        Answer(role);

        (await CreateSut().IsTrustedAsync(Author, default)).Should().BeTrue();
    }

    [Fact]
    public async Task GitHubTrust_ReadOnlyCollaborator_Untrusted()
    {
        Answer("read", "read");

        (await CreateSut().IsTrustedAsync(Author, default)).Should().BeFalse();
    }

    [Fact]
    public async Task GitHubTrust_CustomRoleOverWrite_TrustedThroughLegacyPermission()
    {
        Answer("release-manager", "write");

        (await CreateSut().IsTrustedAsync(Author, default)).Should().BeTrue();
    }

    [Fact]
    public async Task GitHubTrust_UnknownUser_Untrusted()
    {
        Answer();

        (await CreateSut().IsTrustedAsync(Author, default)).Should().BeFalse();
    }

    [Fact]
    public async Task GitHubTrust_UnconfiguredRepository_Untrusted()
    {
        (await CreateSut(configuredUrl: null).IsTrustedAsync(Author, default)).Should().BeFalse();

        _permissions.Verify(p => p.ReadAsync(It.IsAny<RepoConnection>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GitHubTrust_LookupFails_Untrusted()
    {
        _permissions.Setup(p => p.ReadAsync(It.IsAny<RepoConnection>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));

        (await CreateSut().IsTrustedAsync(Author, default)).Should().BeFalse();
    }

    [Fact]
    public async Task GitHubTrust_Twice_AsksGitHubOnce()
    {
        Answer("write");
        var sut = CreateSut();

        await sut.IsTrustedAsync(Author, default);
        await sut.IsTrustedAsync(Author, default);

        _permissions.Verify(p => p.ReadAsync(It.IsAny<RepoConnection>(), "dev", It.IsAny<CancellationToken>()), Times.Once);
    }
}
