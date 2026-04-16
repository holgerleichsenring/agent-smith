using AgentSmith.Cli.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Webhooks;

public sealed class GitHubPrCommentWebhookHandlerTests
{
    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void CanHandle_CorrectEventTypes()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);

        sut.CanHandle("github", "issue_comment").Should().BeTrue();
        sut.CanHandle("github", "pull_request_review_comment").Should().BeTrue();
        sut.CanHandle("github", "issues").Should().BeFalse();
        sut.CanHandle("gitlab", "issue_comment").Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_FixCommand_ReturnsPipeline()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 100,
                "body": "/agent-smith fix",
                "user": { "login": "dev-user" },
                "author_association": "MEMBER"
            },
            "issue": {
                "number": 42,
                "pull_request": { "url": "https://api.github.com/repos/org/my-api/pulls/42" }
            },
            "repository": { "full_name": "org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Be("fix-bug pr:org/my-api#42");
        result.Pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public async Task HandleAsync_FixWithArguments_ReturnsArguments()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 101,
                "body": "/agent-smith fix #123 in my-api",
                "user": { "login": "dev-user" },
                "author_association": "MEMBER"
            },
            "issue": {
                "number": 42,
                "pull_request": { "url": "https://api.github.com/repos/org/my-api/pulls/42" }
            },
            "repository": { "full_name": "org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Be("fix-bug #123 in my-api");
        result.Pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public async Task HandleAsync_SecurityScan_ReturnsSecurityPipeline()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 102,
                "body": "/agent-smith security-scan",
                "user": { "login": "dev-user" },
                "author_association": "MEMBER"
            },
            "issue": {
                "number": 7,
                "pull_request": { "url": "https://api.github.com/repos/org/my-api/pulls/7" }
            },
            "repository": { "full_name": "org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Be("security-scan pr:org/my-api#7");
        result.Pipeline.Should().Be("security-scan");
    }

    [Fact]
    public async Task HandleAsync_Help_ReturnsNotHandled()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 103,
                "body": "/agent-smith help",
                "user": { "login": "dev-user" },
                "author_association": "MEMBER"
            },
            "issue": {
                "number": 42,
                "pull_request": { "url": "https://api.github.com/repos/org/my-api/pulls/42" }
            },
            "repository": { "full_name": "org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_Approve_ReturnsDialogueAnswer()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 104,
                "body": "/approve looks good",
                "user": { "login": "dev-user" },
                "author_association": "MEMBER"
            },
            "issue": {
                "number": 42,
                "pull_request": { "url": "https://api.github.com/repos/org/my-api/pulls/42" }
            },
            "repository": { "full_name": "org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.DialogueAnswer.Should().NotBeNull();
        result.DialogueAnswer!.Answer.Should().Be("yes");
    }

    [Fact]
    public async Task HandleAsync_PlainIssueComment_ReturnsNotHandled()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 105,
                "body": "/agent-smith fix",
                "user": { "login": "dev-user" },
                "author_association": "MEMBER"
            },
            "issue": {
                "number": 42
            },
            "repository": { "full_name": "org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_EditedAction_ReturnsNotHandled()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "edited",
            "comment": {
                "id": 106,
                "body": "/agent-smith fix",
                "user": { "login": "dev-user" },
                "author_association": "MEMBER"
            },
            "issue": {
                "number": 42,
                "pull_request": { "url": "https://api.github.com/repos/org/my-api/pulls/42" }
            },
            "repository": { "full_name": "org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_ReviewComment_ReturnsHandled()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 107,
                "body": "/as fix",
                "user": { "login": "dev-user" },
                "author_association": "COLLABORATOR"
            },
            "pull_request": { "number": 10 },
            "repository": { "full_name": "org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Be("fix-bug pr:org/my-api#10");
        result.Pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public async Task HandleAsync_UnknownCommand_ReturnsNotHandled()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 108,
                "body": "Just a regular comment",
                "user": { "login": "dev-user" },
                "author_association": "MEMBER"
            },
            "issue": {
                "number": 42,
                "pull_request": { "url": "https://api.github.com/repos/org/my-api/pulls/42" }
            },
            "repository": { "full_name": "org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }
}
