using System.Net;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Moq;
using Octokit;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// p0298: GitHub's "already exists" 422 (returned on a re-run whose branch already
/// has an open PR) must be detected so CreatePullRequestAsync reuses the existing PR
/// instead of failing. This pins the provider-specific detection; the thin
/// GetAllForRepository→HtmlUrl reuse mirrors the wire-tested GitLab/AzDO providers.
/// </summary>
public sealed class GitHubSourceProviderCreatePrTests
{
    [Fact]
    public void PullRequestAlreadyExists_ErrorMentionsAlreadyExists_ReturnsTrue()
    {
        var ex = ApiValidation("A pull request already exists for example:agent-smith/SCR-431.");

        GitHubSourceProvider.PullRequestAlreadyExists(ex).Should().BeTrue();
    }

    [Fact]
    public void PullRequestAlreadyExists_OtherValidationError_ReturnsFalse()
    {
        var ex = ApiValidation("base ref must be a branch");

        GitHubSourceProvider.PullRequestAlreadyExists(ex).Should().BeFalse();
    }

    private static ApiValidationException ApiValidation(string errorMessage)
    {
        var body = $$"""
            {"message":"Validation Failed","errors":[{"resource":"PullRequest","code":"custom","message":"{{errorMessage}}"}]}
            """;
        var response = new Mock<IResponse>();
        response.SetupGet(r => r.Body).Returns(body);
        response.SetupGet(r => r.StatusCode).Returns(HttpStatusCode.UnprocessableEntity);
        response.SetupGet(r => r.ContentType).Returns("application/json");
        response.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());
        return new ApiValidationException(response.Object);
    }
}
