using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-11-25-7a92 series: the Application layer reaches a pull-request capability by casting the provider the REAL factory
/// builds, so parity is pinned there — not on a hand-made provider.
/// </summary>
public sealed class SourceProviderParityTests
{
    private static readonly SourceProviderFactory Factory = new(
        Mock.Of<ICredentialResolver>(c => c.For(It.IsAny<RepoConnection>()) == "token"), Mock.Of<IHttpClientFactory>(),
        Mock.Of<IGitHubClientFactory>(), Mock.Of<IAzDoClientFactory>(), NullLoggerFactory.Instance);

    [Theory]
    [InlineData(RepoType.GitHub, "https://github.com/example/sample-server")]
    [InlineData(RepoType.GitLab, "https://gitlab.example.com/group/sample-server")]
    [InlineData(RepoType.AzureDevOps, "https://dev.azure.com/example/sample/_git/sample-server")]
    public void Create_EveryHost_RequeuesNotStartedBuilds(RepoType type, string url) =>
        Provider(type, url).Should().BeAssignableTo<IPullRequestBuildRequeuer>();

    internal static ISourceProvider Provider(RepoType type, string url) =>
        Factory.Create(new RepoConnection { Name = "sample", Type = type, Url = url, Auth = "repo_token" });
}
