using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// Bug fix: AzureReposSourceConnection / AzureReposSourceProvider previously
/// ignored the catalog's default_branch value and always queried the Azure
/// DevOps API for the repo-side default. Brings the Azure path in line with
/// GitHubSourceConnection / GitLabSourceConnection, both of which honor a
/// configured override before any API lookup.
/// </summary>
public sealed class AzureReposSourceProviderDefaultBranchTests
{
    private const string OrgUrl = "https://dev.azure.com/example";
    private const string Project = "demo";
    private const string Repo = "repo";
    private const string Pat = "azdo-pat";

    [Fact]
    public async Task CheckoutAsync_ConfiguredDefaultBranch_UsedWithoutCallingAzureDevOpsApi()
    {
        var factoryMock = new Mock<IAzDoClientFactory>(MockBehavior.Strict);
        var sut = new AzureReposSourceProvider(
            new AzureReposSourceConnection(OrgUrl, Project, Repo, Pat, DefaultBranch: "develop"),
            factoryMock.Object,
            NullLogger<AzureReposSourceProvider>.Instance);

        var repo = await sut.CheckoutAsync(branch: null, CancellationToken.None);

        repo.CurrentBranch.Value.Should().Be("develop");
        factoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CheckoutAsync_ExplicitBranchArgument_OverridesConfiguredDefault()
    {
        var factoryMock = new Mock<IAzDoClientFactory>(MockBehavior.Strict);
        var sut = new AzureReposSourceProvider(
            new AzureReposSourceConnection(OrgUrl, Project, Repo, Pat, DefaultBranch: "develop"),
            factoryMock.Object,
            NullLogger<AzureReposSourceProvider>.Instance);

        var repo = await sut.CheckoutAsync(new AgentSmith.Domain.Models.BranchName("feature/x"), CancellationToken.None);

        repo.CurrentBranch.Value.Should().Be("feature/x");
        factoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CheckoutAsync_NoConfiguredDefault_FallsBackToCachedApiResult()
    {
        // Pre-seed the cache to assert the legacy path (configured=null → API → cache) still works.
        var factoryMock = new Mock<IAzDoClientFactory>(MockBehavior.Strict);
        var sut = new AzureReposSourceProvider(
            new AzureReposSourceConnection(OrgUrl, Project, Repo, Pat, DefaultBranch: null),
            factoryMock.Object,
            NullLogger<AzureReposSourceProvider>.Instance);
        typeof(AzureReposSourceProvider)
            .GetField("_cachedDefaultBranch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(sut, "main");

        var repo = await sut.CheckoutAsync(branch: null, CancellationToken.None);

        repo.CurrentBranch.Value.Should().Be("main");
        factoryMock.VerifyNoOtherCalls();
    }
}
