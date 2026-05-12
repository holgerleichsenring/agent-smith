using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;
using Moq.Language.Flow;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// p0135 follow-up: AzureReposSourceProvider.TryReadFileAsync — calls
/// GitHttpClient.GetItemContentAsync against the default branch, returns
/// null when the SDK reports the item is missing (the AzDo SDK signals 404
/// as a VssServiceException whose message contains "could not be found" /
/// "does not exist"). Auth + server errors propagate.
/// </summary>
public sealed class AzureReposSourceProviderTryReadFileTests
{
    private const string OrgUrl = "https://dev.azure.com/example";
    private const string Project = "demo";
    private const string Repo = "repo";
    private const string Pat = "azdo-pat";
    private const string DefaultBranch = "main";
    private const string Path = ".agentsmith/context.yaml";

    [Fact]
    public async Task TryReadFileAsync_FileExists_ReturnsContent()
    {
        const string body = "stack:\n  lang: C#\n";
        var gitClientMock = NewGitClientMock();
        SetupGetItemContentAsync(gitClientMock)
            .ReturnsAsync(() => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body)));

        var sut = CreateSut(gitClientMock.Object);

        var result = await sut.TryReadFileAsync(Path, CancellationToken.None);

        result.Should().Be(body);
    }

    [Fact]
    public async Task TryReadFileAsync_NotFoundShapedException_ReturnsNull()
    {
        // AzDo SDK doesn't have a typed NotFoundException — 404s come back as
        // VssServiceException with the message containing "could not be found"
        // or "does not exist". The provider matches on that message.
        var gitClientMock = NewGitClientMock();
        SetupGetItemContentAsync(gitClientMock)
            .ThrowsAsync(new VssServiceException("TF401174: The item .agentsmith/context.yaml could not be found in the repository at version refs/heads/main."));

        var sut = CreateSut(gitClientMock.Object);

        var result = await sut.TryReadFileAsync(Path, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task TryReadFileAsync_ServerError_Throws()
    {
        // VssServiceException whose message does NOT carry the not-found marker
        // must propagate — the SDK uses VssServiceException for many server-side
        // failure modes (rate-limit, repository disabled, transient 5xx).
        var gitClientMock = NewGitClientMock();
        SetupGetItemContentAsync(gitClientMock)
            .ThrowsAsync(new VssServiceException("VS800075: Internal server error processing your request."));

        var sut = CreateSut(gitClientMock.Object);

        var act = async () => await sut.TryReadFileAsync(Path, CancellationToken.None);

        await act.Should().ThrowAsync<VssServiceException>();
    }

    [Fact]
    public async Task TryReadFileAsync_AuthError_Throws()
    {
        // 401 / 403 come through as VssUnauthorizedException — surface it so the
        // operator sees a misconfigured PAT instead of a silent fall-through to
        // GenericFallback.
        var gitClientMock = NewGitClientMock();
        SetupGetItemContentAsync(gitClientMock)
            .ThrowsAsync(new VssUnauthorizedException("TF400813: Resource not available for anonymous access."));

        var sut = CreateSut(gitClientMock.Object);

        var act = async () => await sut.TryReadFileAsync(Path, CancellationToken.None);

        await act.Should().ThrowAsync<VssUnauthorizedException>();
    }

    // Matches the non-obsolete GetItemContentAsync(string project, string repositoryId, ...)
    // overload on GitHttpClientBase — 14 parameters; recursionLevel + every boolean
    // flag is nullable. Pinning the exact signature here keeps the four [Fact]s
    // readable. (The obsolete 13-param variant lacks `sanitize` — picking it would
    // trigger CS0612 and bind to the wrong overload at runtime.)
    private static ISetup<GitHttpClient, Task<Stream>> SetupGetItemContentAsync(Mock<GitHttpClient> gitClientMock)
    {
        return gitClientMock.Setup(c => c.GetItemContentAsync(
            It.IsAny<string>(),                              // project
            It.IsAny<string>(),                              // repositoryId
            It.IsAny<string>(),                              // path
            It.IsAny<string>(),                              // scopePath
            It.IsAny<VersionControlRecursionType?>(),        // recursionLevel
            It.IsAny<bool?>(),                               // includeContentMetadata
            It.IsAny<bool?>(),                               // latestProcessedChange
            It.IsAny<bool?>(),                               // download
            It.IsAny<GitVersionDescriptor>(),                // versionDescriptor
            It.IsAny<bool?>(),                               // includeContent
            It.IsAny<bool?>(),                               // resolveLfs
            It.IsAny<bool?>(),                               // sanitize
            It.IsAny<object>(),                              // userState
            It.IsAny<CancellationToken>()));                 // cancellationToken
    }

    private static Mock<GitHttpClient> NewGitClientMock()
    {
        // GitHttpClient is a concrete class; Moq needs constructor args even
        // though every SDK call site goes through the Setup'd virtual method.
        // The ctor takes VssCredentials (not VssBasicCredential directly) —
        // wrap the basic-credential in a VssCredentials envelope.
        return new Mock<GitHttpClient>(
            new Uri("https://localhost/fake"),
            new VssCredentials(new VssBasicCredential(string.Empty, "fake")));
    }

    private static AzureReposSourceProvider CreateSut(GitHttpClient gitClient)
    {
        var factoryMock = new Mock<IAzDoClientFactory>();
        factoryMock.Setup(f => f.CreateGitClient(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(gitClient);

        // GetDefaultBranchAsync also goes through CreateGitClient (calls
        // GetRepositoryAsync). Short-circuit by pre-seeding the cached default
        // branch via the private field — alternative is mocking GetRepositoryAsync
        // with its full GitRepository return shape, which buys nothing here.
        var sut = new AzureReposSourceProvider(
            OrgUrl, Project, Repo, Pat,
            factoryMock.Object,
            NullLogger<AzureReposSourceProvider>.Instance);
        var field = typeof(AzureReposSourceProvider)
            .GetField("_cachedDefaultBranch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(sut, DefaultBranch);
        return sut;
    }
}
