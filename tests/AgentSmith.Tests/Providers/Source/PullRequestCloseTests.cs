using System.Net;
using System.Text;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using Octokit;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-10-15-114fa: each remote host closes a pull request without merging it, reached through the provider as
/// <see cref="IPullRequestCloser"/>; a refusing host is thrown, for the caller to class. The local provider has none.
/// </summary>
public sealed class PullRequestCloseTests
{
    [Fact]
    public async Task GitHubPullRequestClose_UpdatesTheStateToClosed()
    {
        var pullRequests = new Mock<IPullRequestsClient>();
        PullRequestUpdate? sent = null;
        pullRequests.Setup(c => c.Update("example", "sample-server", 7, It.IsAny<PullRequestUpdate>()))
            .Callback<string, string, int, PullRequestUpdate>((_, _, _, update) => sent = update)
            .ReturnsAsync(new PullRequest());
        var client = new Mock<IGitHubClient>();
        client.SetupGet(c => c.PullRequest).Returns(pullRequests.Object);
        var factory = new Mock<IGitHubClientFactory>();
        factory.Setup(f => f.Create("ghp-test")).Returns(client.Object);
        var provider = new GitHubSourceProvider(new GitHubSourceConnection("https://github.com/example/sample-server", "ghp-test", "main"),
            factory.Object, NullLogger<GitHubSourceProvider>.Instance);

        await ((IPullRequestCloser)provider).CloseAsync("https://github.com/example/sample-server/pull/7", CancellationToken.None);

        sent!.State.Should().Be(ItemState.Closed);
    }

    [Fact]
    public async Task GitLabMergeRequestClose_PutsStateEventClose()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK);
        var provider = new GitLabSourceProvider(new GitLabSourceConnection("https://gitlab.example.com", "group%2Frepo",
            "https://gitlab.example.com/group/repo.git", "glpat-test", "main"), new HttpClient(handler), NullLogger<GitLabSourceProvider>.Instance);

        await ((IPullRequestCloser)provider).CloseAsync("https://gitlab.example.com/group/repo/-/merge_requests/3", CancellationToken.None);

        handler.Method.Should().Be(HttpMethod.Put);
        handler.Uri!.ToString().Should().Be("https://gitlab.example.com/api/v4/projects/group%2Frepo/merge_requests/3");
        handler.Body.Should().Contain("\"state_event\":\"close\"");
        handler.Token.Should().Be("glpat-test");
    }

    [Fact]
    public async Task GitLabMergeRequestClose_HostRefuses_Throws()
    {
        var provider = new GitLabSourceProvider(new GitLabSourceConnection("https://gitlab.example.com", "group%2Frepo",
            "https://gitlab.example.com/group/repo.git", "glpat-test", "main"), new HttpClient(new CapturingHandler(HttpStatusCode.MethodNotAllowed)),
            NullLogger<GitLabSourceProvider>.Instance);

        var close = () => ((IPullRequestCloser)provider).CloseAsync("https://gitlab.example.com/group/repo/-/merge_requests/3", CancellationToken.None);

        (await close.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task AzureReposPullRequestClose_SetsStatusAbandoned()
    {
        var git = new Mock<GitHttpClient>(new Uri("https://localhost/fake"), new VssCredentials(new VssBasicCredential(string.Empty, "fake")));
        GitPullRequest? sent = null;
        git.Setup(c => c.UpdatePullRequestAsync(It.IsAny<GitPullRequest>(), "demo", "sample-server", 7, It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<GitPullRequest, string, string, int, object, CancellationToken>((update, _, _, _, _, _) => sent = update)
            .ReturnsAsync(new GitPullRequest());
        var factory = new Mock<IAzDoClientFactory>();
        factory.Setup(f => f.CreateGitClient(It.IsAny<string>(), It.IsAny<string>())).Returns(git.Object);
        var provider = new AzureReposSourceProvider(new AzureReposSourceConnection("https://dev.azure.com/example", "demo", "sample-server", "pat", "main"),
            factory.Object, NullLogger<AzureReposSourceProvider>.Instance);

        await ((IPullRequestCloser)provider).CloseAsync("https://dev.azure.com/example/demo/_git/sample-server/pullrequest/7", CancellationToken.None);

        sent!.Status.Should().Be(PullRequestStatus.Abandoned);
    }

    [Fact]
    public void LocalSourceProvider_ClosesNoPullRequest() =>
        typeof(LocalSourceProvider).IsAssignableTo(typeof(IPullRequestCloser)).Should().BeFalse();

    private sealed class CapturingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? Uri { get; private set; }
        public string? Body { get; private set; }
        public string? Token { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            (Method, Uri) = (request.Method, request.RequestUri);
            Token = request.Headers.TryGetValues("PRIVATE-TOKEN", out var values) ? values.Single() : null;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        }
    }
}
