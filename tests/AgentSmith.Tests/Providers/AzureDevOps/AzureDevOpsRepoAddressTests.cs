using System.Net;
using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Providers.AzureDevOps;

/// <summary>
/// 2026-10-26-cb25a: the characterisation matrix of the five Azure Repos URL parsers, now run against their one
/// home. Each row carries what the copies did before ("was") — src = SourceProviderFactory, diff =
/// PrDiffProviderFactory, trust = AzureDevOpsRepoContributeTrust, webhook = AzureDevOpsPrUrl — so a changed outcome
/// is read off the row; decisions/2026-10-26-cb25a.yaml says why each one changed.
/// </summary>
public sealed class AzureDevOpsRepoAddressTests
{
    public static TheoryData<string, string?, string?, string?, string> Matrix => new()
    {
        { "https://dev.azure.com/org/proj/_git/repo", "https://dev.azure.com/org", "proj", "repo", "all four agreed" },
        { "https://dev.azure.com/org/proj/_git/repo.git", "https://dev.azure.com/org", "proj", "repo", "agreed; webhook kept .git" },
        { "https://org.visualstudio.com/proj/_git/repo", "https://org.visualstudio.com", "proj", "repo", "src, diff threw; trust parsed" },
        { "https://org.visualstudio.com/DefaultCollection/proj/_git/repo", "https://org.visualstudio.com/DefaultCollection", "proj", "repo", "all four agreed" },
        { "https://tfs.example.com/tfs/Coll/Proj/_git/Repo", "https://tfs.example.com/tfs/Coll", "Proj", "Repo", "src, diff threw; trust parsed" },
        { "https://tfs.example.com:8080/tfs/Coll/Proj/_git/Repo", "https://tfs.example.com:8080/tfs/Coll", "Proj", "Repo", "src, diff threw; trust kept the port; webhook dropped it" },
        { "https://tfs.example.com:8080/Coll/Proj/_git/Repo", "https://tfs.example.com:8080/Coll", "Proj", "Repo", "src, diff, webhook dropped the port; trust kept it" },
        { "https://dev.azure.com/org/proj/_GIT/repo", "https://dev.azure.com/org", "proj", "repo", "src, diff threw on the case; trust parsed" },
        { "https://user@dev.azure.com/org/proj/_git/repo", "https://dev.azure.com/org", "proj", "repo", "all four dropped the user info" },
        { "https://dev.azure.com/org/My%20Proj/_git/My%20Repo", "https://dev.azure.com/org", "My%20Proj", "My%20Repo", "all kept the escaped form" },
        { "https://dev.azure.com/org/proj/_git/repo/pullrequest/5", "https://dev.azure.com/org", "proj", "repo", "agreed; webhook kept the tail" },
        { "https://dev.azure.com/org/_git/repo", null, null, null, "src, diff threw; trust read organization https://dev.azure.com" },
        { "https://github.com/o/r", null, null, null, "src, diff, trust threw" },
        { "not a url", null, null, null, "src, diff, trust threw" },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public void TryParse_TheMatrix(string url, string? organizationUrl, string? project, string? repo, string was)
    {
        var parsed = AzureDevOpsRepoAddress.TryParse(url, out var address);

        parsed.Should().Be(organizationUrl is not null, was);
        address?.OrganizationUrl.Should().Be(organizationUrl, was);
        address?.Project.Should().Be(project, was);
        address?.Repo.Should().Be(repo, was);
    }

    [Fact]
    public void Parse_NotARepositoryUrl_IsAConfigurationError() =>
        FluentActions.Invoking(() => AzureDevOpsRepoAddress.Parse("https://dev.azure.com/org/_git/repo"))
            .Should().Throw<AgentSmith.Domain.Exceptions.ConfigurationException>();

    [Theory]
    [InlineData("https://user@tfs.example.com:8080/tfs/Coll/Proj/_GIT/Repo.git", "https://tfs.example.com:8080/tfs/Coll/Proj/_git/Repo")]
    [InlineData("https://org@dev.azure.com/org/proj/_git/repo", "https://dev.azure.com/org/proj/_git/repo")]
    [InlineData("https://dev.azure.com/org/_git/repo/", "https://dev.azure.com/org/_git/repo")]
    public void WebhookRepositoryUrl_IsTheAddressRepoUrl_OrTheRemoteAsWritten(string remoteUrl, string expected)
    {
        using var pr = JsonDocument.Parse(JsonSerializer.Serialize(new { repository = new { remoteUrl } }));

        AzureDevOpsPrUrl.RepositoryUrl(pr.RootElement).Should().Be(expected);
    }

    [Theory]
    [InlineData("https://dev.azure.com/org", "org")]
    [InlineData("https://dev.azure.com/org/proj/_workitems/edit/1", "org")]
    [InlineData("https://ORG.visualstudio.com/proj", "org")]
    [InlineData("https://tfs.example.com/tfs/Coll", null)]
    [InlineData("x", null)]
    public void CloudOrganizationOf_TheWorkItemLinkMatrix(string url, string? expected) =>
        AzureDevOpsOrganizationUrl.CloudOrganizationOf(url).Should().Be(expected);

    [Fact]
    public async Task PrDiffFactory_ServerUrlWithPort_AddressesTheCollection()
    {
        var requested = new List<string>();
        var http = new Mock<IHttpClientFactory>();
        http.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(new RecordingHandler(requested)));
        var credentials = new Mock<ICredentialResolver>();
        credentials.Setup(c => c.For(It.IsAny<RepoConnection>())).Returns("pat");
        var factory = new PrDiffProviderFactory(credentials.Object, http.Object, Mock.Of<IGitHubClientFactory>(), NullLoggerFactory.Instance);

        await factory.Create(new RepoConnection
        {
            Name = "r", Type = RepoType.AzureDevOps, Url = "https://tfs.example.com:8080/tfs/Coll/Proj/_git/Repo",
        }).GetDiffAsync("9");

        requested.Should().HaveCount(3).And.AllSatisfy(u =>
            u.Should().StartWith("https://tfs.example.com:8080/tfs/Coll/Proj/_apis/git/repositories/Repo/pullRequests/9"));
    }

    private sealed class RecordingHandler(List<string> requested) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            requested.Add(url);
            var body = url.Contains("/changes", StringComparison.Ordinal) ? """{"changeEntries":[]}"""
                : url.Contains("/iterations", StringComparison.Ordinal) ? """{"count":1}"""
                : """{"lastMergeTargetCommit":{"commitId":"b"},"lastMergeSourceCommit":{"commitId":"h"}}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
