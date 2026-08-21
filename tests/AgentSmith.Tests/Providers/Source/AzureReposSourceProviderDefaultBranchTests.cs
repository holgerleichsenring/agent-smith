using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Moq;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// p0500: these tests previously pinned the OPPOSITE contract — "a configured
/// default_branch wins, without calling the Azure DevOps API" — which is the defect,
/// not a feature. config/agentsmith.example.yml says the branch comes from each
/// discovered repo and the connection value is only a fallback, and reading a repo on
/// a branch it does not have makes every read fail (TF401175), so discovery sees an
/// empty repository and init-project opens no pull request.
/// <para>
/// They are rewritten rather than deleted: the wiring they cover — that whatever the
/// resolver decides actually reaches CheckoutAsync — still has to hold.
/// </para>
/// </summary>
public sealed class AzureReposSourceProviderDefaultBranchTests
{
    private const string OrgUrl = "https://dev.azure.com/example";
    private const string Project = "demo";
    private const string Repo = "repo";
    private const string Pat = "azdo-pat";

    [Fact]
    public async Task CheckoutAsync_RepositoryDefaultBranch_WinsOverTheConfiguredValue()
    {
        var sut = Build(configured: "develop", repositoryDefault: "refs/heads/main");

        var repo = await sut.CheckoutAsync(branch: null, CancellationToken.None);

        repo.CurrentBranch.Value.Should().Be(
            "main", "the repository has no develop; reading it on develop is what blinded it");
    }

    [Fact]
    public async Task CheckoutAsync_RepositoryAnswersNothing_FallsBackToTheConfiguredValue()
    {
        var sut = Build(configured: "develop", repositoryDefault: null);

        var repo = await sut.CheckoutAsync(branch: null, CancellationToken.None);

        repo.CurrentBranch.Value.Should().Be("develop");
    }

    [Fact]
    public async Task CheckoutAsync_NothingConfigured_UsesTheRepositoryDefault()
    {
        var sut = Build(configured: null, repositoryDefault: "refs/heads/trunk");

        var repo = await sut.CheckoutAsync(branch: null, CancellationToken.None);

        repo.CurrentBranch.Value.Should().Be("trunk");
    }

    [Fact]
    public async Task CheckoutAsync_ExplicitBranchArgument_StillWins()
    {
        var factoryMock = new Mock<IAzDoClientFactory>(MockBehavior.Strict);
        var sut = new AzureReposSourceProvider(
            new AzureReposSourceConnection(OrgUrl, Project, Repo, Pat, DefaultBranch: "develop"),
            factoryMock.Object, NullLogger<AzureReposSourceProvider>.Instance);

        var repo = await sut.CheckoutAsync(new AgentSmith.Domain.Models.BranchName("feature/x"), CancellationToken.None);

        repo.CurrentBranch.Value.Should().Be("feature/x");
        // An explicit branch needs no lookup at all — the strict mock proves none happened.
        factoryMock.VerifyNoOtherCalls();
    }

    private static AzureReposSourceProvider Build(string? configured, string? repositoryDefault)
    {
        var gitClientMock = new Mock<GitHttpClient>(
            new Uri("https://localhost/fake"),
            new VssCredentials(new VssBasicCredential(string.Empty, "fake")));
        gitClientMock.Setup(c => c.GetRepositoryAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitRepository { DefaultBranch = repositoryDefault });

        var factoryMock = new Mock<IAzDoClientFactory>();
        factoryMock.Setup(f => f.CreateGitClient(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(gitClientMock.Object);

        return new AzureReposSourceProvider(
            new AzureReposSourceConnection(OrgUrl, Project, Repo, Pat, DefaultBranch: configured),
            factoryMock.Object, NullLogger<AzureReposSourceProvider>.Instance);
    }
}
