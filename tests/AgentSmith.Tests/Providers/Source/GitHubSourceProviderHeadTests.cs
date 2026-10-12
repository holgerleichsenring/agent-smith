using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Octokit;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>GitHub's issue_comment carries no head, so a command's pull request is read for it.</summary>
public sealed class GitHubSourceProviderHeadTests
{
    private const string Token = "ghp-test";
    private readonly Mock<IPullRequestsClient> _pullRequests = new();

    [Theory]
    [InlineData("example/sample-app", true)]
    [InlineData("stranger/sample-app", false)]
    public async Task HeadAsync_ReadsBranchForkAndUrl(string headRepo, bool same)
    {
        _pullRequests.Setup(c => c.Get("example", "sample-app", 42)).ReturnsAsync(Pr(headRepo));

        var head = await ((IPullRequestHeadReader)CreateSut()).HeadAsync("42", CancellationToken.None);

        head.Should().Be(new PullRequestHead("agent-smith/42", same, "https://github.com/example/sample-app/pull/42"));
    }

    private static PullRequest Pr(string headRepo)
    {
        var repository = new Octokit.Repository();
        Set(repository, nameof(Octokit.Repository.FullName), headRepo);
        var head = new GitReference();
        Set(head, nameof(GitReference.Ref), "agent-smith/42");
        Set(head, nameof(GitReference.Repository), repository);
        var pr = new PullRequest(42);
        Set(pr, nameof(PullRequest.Head), head);
        Set(pr, nameof(PullRequest.HtmlUrl), "https://github.com/example/sample-app/pull/42");
        return pr;
    }

    // Octokit's response models have protected setters; the provider only reads them.
    private static void Set(object target, string property, object value) =>
        target.GetType().GetProperty(property)!.GetSetMethod(nonPublic: true)!.Invoke(target, [value]);

    private ISourceProvider CreateSut()
    {
        var client = new Mock<IGitHubClient>();
        client.SetupGet(c => c.PullRequest).Returns(_pullRequests.Object);
        var factory = new Mock<IGitHubClientFactory>();
        factory.Setup(f => f.Create(Token)).Returns(client.Object);
        return new GitHubSourceProvider(new GitHubSourceConnection("https://github.com/example/sample-app", Token, "main"),
            factory.Object, NullLogger<GitHubSourceProvider>.Instance);
    }
}
