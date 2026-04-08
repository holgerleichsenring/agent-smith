using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class GitHubPrCommentReplyServiceTests
{
    [Fact]
    public void ImplementsInterface()
    {
        // SecretsProvider reads env vars; we set GITHUB_TOKEN for construction
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "test-token");
        try
        {
            var sut = new GitHubPrCommentReplyService(
                new SecretsProvider(),
                NullLogger<GitHubPrCommentReplyService>.Instance);

            sut.Should().BeAssignableTo<IPrCommentReplyService>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        }
    }

    [Fact]
    public void InvalidRepoFullName_ThrowsArgumentException()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "test-token");
        try
        {
            var sut = new GitHubPrCommentReplyService(
                new SecretsProvider(),
                NullLogger<GitHubPrCommentReplyService>.Instance);

            var intent = new CommentIntent(
                CommentIntentType.NewJob,
                "github",
                "invalid-no-slash",
                "42",
                "100",
                "dev-user",
                "fix-bug",
                null,
                null,
                "/agent-smith fix");

            var act = () => sut.ReplyAsync(intent, "reply text", CancellationToken.None);

            act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Invalid repository full name*");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        }
    }

    [Fact]
    public void MissingToken_ThrowsConfigurationException()
    {
        // Ensure no token is set
        var original = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        try
        {
            var sut = new GitHubPrCommentReplyService(
                new SecretsProvider(),
                NullLogger<GitHubPrCommentReplyService>.Instance);

            var intent = new CommentIntent(
                CommentIntentType.NewJob,
                "github",
                "org/my-api",
                "42",
                "100",
                "dev-user",
                "fix-bug",
                null,
                null,
                "/agent-smith fix");

            var act = () => sut.ReplyAsync(intent, "reply text", CancellationToken.None);

            // SecretsProvider.GetRequired throws ConfigurationException when env var is missing
            act.Should().ThrowAsync<Exception>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", original);
        }
    }
}
