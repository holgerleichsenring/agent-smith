using System.Net;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Octokit;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// p0135 follow-up: GitHubSourceProvider.TryReadFileAsync — calls Octokit's
/// Repository.Content.GetAllContentsByRef on the default branch, returns
/// null on NotFoundException (404), propagates auth + server errors.
/// </summary>
public sealed class GitHubSourceProviderTryReadFileTests
{
    private const string RepoUrl = "https://github.com/example/repo";
    private const string Token = "ghp-test";
    private const string DefaultBranch = "main";
    private const string Path = ".agentsmith/context.yaml";

    [Fact]
    public async Task TryReadFileAsync_FileExists_ReturnsDecodedContent()
    {
        const string body = "stack:\n  lang: C#\n";
        var item = NewRepositoryContent(body);

        var contentsMock = new Mock<IRepositoryContentsClient>();
        contentsMock
            .Setup(c => c.GetAllContentsByRef("example", "repo", Path, DefaultBranch))
            .ReturnsAsync(new[] { item });

        var sut = CreateSut(contentsMock.Object);

        var result = await sut.TryReadFileAsync(Path, CancellationToken.None);

        result.Should().Be(body);
    }

    [Fact]
    public async Task TryReadFileAsync_NotFoundException_ReturnsNull()
    {
        var contentsMock = new Mock<IRepositoryContentsClient>();
        contentsMock
            .Setup(c => c.GetAllContentsByRef(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new NotFoundException("Not Found", HttpStatusCode.NotFound));

        var sut = CreateSut(contentsMock.Object);

        var result = await sut.TryReadFileAsync(Path, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task TryReadFileAsync_ServerError_Throws()
    {
        // 5xx is "GitHub is broken", not "file missing" — must surface so the
        // resolver doesn't silently degrade to GenericFallback when the API
        // itself is degraded.
        var contentsMock = new Mock<IRepositoryContentsClient>();
        contentsMock
            .Setup(c => c.GetAllContentsByRef(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new ApiException("Internal Server Error", HttpStatusCode.InternalServerError));

        var sut = CreateSut(contentsMock.Object);

        var act = async () => await sut.TryReadFileAsync(Path, CancellationToken.None);

        await act.Should().ThrowAsync<ApiException>();
    }

    [Fact]
    public async Task TryReadFileAsync_AuthError_Throws()
    {
        // 401 = bad/missing token. Operator-config error — must throw so the
        // operator sees it rather than silently falling back to generic image.
        var contentsMock = new Mock<IRepositoryContentsClient>();
        contentsMock
            .Setup(c => c.GetAllContentsByRef(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new AuthorizationException());

        var sut = CreateSut(contentsMock.Object);

        var act = async () => await sut.TryReadFileAsync(Path, CancellationToken.None);

        await act.Should().ThrowAsync<AuthorizationException>();
    }

    private static RepositoryContent NewRepositoryContent(string body)
    {
        // RepositoryContent.Content decodes EncodedContent from base64 when
        // Encoding == "base64" — same shape as a real GitHub Contents API response.
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(body));
        return new RepositoryContent(
            name: "context.yaml",
            path: Path,
            sha: "deadbeef",
            size: body.Length,
            type: ContentType.File,
            downloadUrl: "",
            url: "",
            gitUrl: "",
            htmlUrl: "",
            encoding: "base64",
            encodedContent: encoded,
            target: null!,
            submoduleGitUrl: null!);
    }

    private static GitHubSourceProvider CreateSut(IRepositoryContentsClient contentsClient)
    {
        // Mock the Octokit client tree: IGitHubClient → IRepositoriesClient → IRepositoryContentsClient.
        // The provider also calls client.Repository.Get(...) inside GetDefaultBranchAsync; we short-circuit
        // that path by passing defaultBranch via ctor so the Repository.Get call is skipped.
        var repoMock = new Mock<IRepositoriesClient>();
        repoMock.SetupGet(r => r.Content).Returns(contentsClient);

        var clientMock = new Mock<IGitHubClient>();
        clientMock.SetupGet(c => c.Repository).Returns(repoMock.Object);

        var factoryMock = new Mock<IGitHubClientFactory>();
        factoryMock.Setup(f => f.Create(Token)).Returns(clientMock.Object);

        return new GitHubSourceProvider(
            repoUrl: RepoUrl,
            token: Token,
            clientFactory: factoryMock.Object,
            logger: NullLogger<GitHubSourceProvider>.Instance,
            defaultBranch: DefaultBranch);
    }
}
