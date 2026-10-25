using System.Net;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using AgentSmith.Tests.ConfigStudio.DraftChecks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Octokit;

namespace AgentSmith.Tests.Providers.GitHub;

/// <summary>2026-10-25-9dbbb: a GitHub Enterprise Server repository is talked to on its own host.</summary>
public sealed class GitHubEnterpriseHostTests
{
    private const string RepoUrl = "https://ghe.example/acme/app";

    [Fact]
    public async Task GitHubTicketStatusTransitioner_EnterpriseUrl_PatchesEnterpriseApi()
    {
        var handler = new RecordingHandler();
        var sut = new GitHubTicketStatusTransitioner(new GitHubTicketConnection(RepoUrl, "token"),
            new HttpClient(handler), NullLogger<GitHubTicketStatusTransitioner>.Instance);

        await sut.TransitionAsync(new TicketId("42"), TicketLifecycleStatus.Pending,
            TicketLifecycleStatus.Enqueued, CancellationToken.None);

        handler.Requests.Should().Equal(
            "GET https://ghe.example/api/v3/repos/acme/app/issues/42",
            "PATCH https://ghe.example/api/v3/repos/acme/app/issues/42");
    }

    [Fact]
    public async Task GitHubConnectionDraftCheck_EnterpriseHost_ProbesApiV3()
    {
        var host = new ScriptedHostHandler()
            .On("https://ghe.example/api/v3/user", 200, """{"login":"octo"}""")
            .On("https://ghe.example/api/v3/orgs/acme", 200, """{"login":"acme"}""")
            .On("https://ghe.example/api/v3/orgs/acme/repos", 200, "[]");
        var draft = new ConnectionEntity("gh", "github", "acme", null, "gitlab_a", null, "https://ghe.example");

        var report = await DraftCheckFixtures.Connections(host).RunAsync(draft, CancellationToken.None);

        DraftCheckFixtures.Keys(report).Should().Be("secret:ok,host:ok,identity:ok,scope:ok,repos:ok");
        host.Requests.Select(r => r.Url).Should().OnlyContain(u => u.StartsWith("https://ghe.example/api/v3/"));
    }

    [Fact]
    public async Task GitHubSourceProvider_EnterpriseUrl_BuildsClientOnApiV3AndPostsGraphQlToApiGraphql()
    {
        Uri? posted = null;
        var response = new Mock<IApiResponse<string>>();
        response.SetupGet(r => r.Body).Returns(
            """{ "data": { "repository": { "pullRequest": { "author": { "login": "a" }, "latestReviews": { "nodes": [] } } } } }""");
        var connection = new Mock<IConnection>();
        connection.Setup(c => c.Post<string>(It.IsAny<Uri>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<Uri, object, string, string, IDictionary<string, string>, CancellationToken>((uri, _, _, _, _, _) => posted = uri).ReturnsAsync(response.Object);
        var client = new Mock<IGitHubClient>();
        client.SetupGet(c => c.Connection).Returns(connection.Object);
        GitHubRepoAddress? built = null;
        var factory = new Mock<IGitHubClientFactory>();
        factory.Setup(f => f.Create(It.IsAny<GitHubRepoAddress>(), "t"))
            .Callback<GitHubRepoAddress, string>((a, _) => built = a).Returns(client.Object);
        var sut = new GitHubSourceProvider(new GitHubSourceConnection(RepoUrl, "t", "main"),
            factory.Object, NullLogger<GitHubSourceProvider>.Instance);

        await sut.ChangesRequestedAsync($"{RepoUrl}/pull/4", CancellationToken.None);

        built!.Api.Rest.Should().Be(new Uri("https://ghe.example/api/v3/"));
        posted.Should().Be(new Uri("https://ghe.example/api/graphql"));
    }

    [Fact]
    public void DefaultGitHubClientFactory_EnterpriseAddress_ClientOnApiV3() =>
        new DefaultGitHubClientFactory().Create(GitHubRepoAddress.Parse(RepoUrl), "t").Connection.BaseAddress
            .Should().Be(new Uri("https://ghe.example/api/v3/"));

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add($"{request.Method} {request.RequestUri}");
            var reply = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"labels":[]}""") };
            return Task.FromResult(reply);
        }
    }
}
