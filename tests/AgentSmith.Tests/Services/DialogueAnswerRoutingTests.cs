using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class DialogueAnswerRoutingTests
{
    [Fact]
    public void WebhookResult_WithDialogueAnswer_CarriesData()
    {
        var data = new DialogueAnswerData(
            Platform: "github",
            RepoFullName: "org/my-api",
            PrIdentifier: "42",
            Answer: "yes",
            Comment: "looks good",
            AuthorLogin: "dev-user");

        var result = new WebhookResult(true, null, null, data);

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().BeNull();
        result.Pipeline.Should().BeNull();
        result.DialogueAnswer.Should().NotBeNull();
        result.DialogueAnswer!.Platform.Should().Be("github");
        result.DialogueAnswer.RepoFullName.Should().Be("org/my-api");
        result.DialogueAnswer.PrIdentifier.Should().Be("42");
        result.DialogueAnswer.Answer.Should().Be("yes");
        result.DialogueAnswer.Comment.Should().Be("looks good");
        result.DialogueAnswer.AuthorLogin.Should().Be("dev-user");
    }

    [Fact]
    public void WebhookResult_WithoutDialogueAnswer_DefaultsToNull()
    {
        var result = new WebhookResult(true, "fix-bug pr:org/my-api#42", "fix-bug");

        result.DialogueAnswer.Should().BeNull();
    }

    [Fact]
    public async Task GitHubPrComment_Approve_ReturnsDialogueAnswerData()
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

        var result = await sut.HandleAsync(payload, new Dictionary<string, string>());

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().BeNull();
        result.DialogueAnswer.Should().NotBeNull();
        result.DialogueAnswer!.Platform.Should().Be("github");
        result.DialogueAnswer.RepoFullName.Should().Be("org/my-api");
        result.DialogueAnswer.PrIdentifier.Should().Be("42");
        result.DialogueAnswer.Answer.Should().Be("yes");
        result.DialogueAnswer.Comment.Should().Be("looks good");
        result.DialogueAnswer.AuthorLogin.Should().Be("dev-user");
    }

    [Fact]
    public async Task GitHubPrComment_Reject_ReturnsDialogueAnswerWithNo()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 105,
                "body": "/reject naming is wrong",
                "user": { "login": "reviewer" },
                "author_association": "MEMBER"
            },
            "issue": {
                "number": 10,
                "pull_request": { "url": "https://api.github.com/repos/team/lib/pulls/10" }
            },
            "repository": { "full_name": "team/lib" }
        }
        """;

        var result = await sut.HandleAsync(payload, new Dictionary<string, string>());

        result.Handled.Should().BeTrue();
        result.DialogueAnswer.Should().NotBeNull();
        result.DialogueAnswer!.Answer.Should().Be("no");
        result.DialogueAnswer.Comment.Should().Be("naming is wrong");
        result.DialogueAnswer.AuthorLogin.Should().Be("reviewer");
        result.DialogueAnswer.RepoFullName.Should().Be("team/lib");
        result.DialogueAnswer.PrIdentifier.Should().Be("10");
    }

    [Fact]
    public async Task GitHubPrComment_ApproveWithoutComment_HasNullComment()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 106,
                "body": "/approve",
                "user": { "login": "dev-user" },
                "author_association": "MEMBER"
            },
            "issue": {
                "number": 5,
                "pull_request": { "url": "https://api.github.com/repos/org/api/pulls/5" }
            },
            "repository": { "full_name": "org/api" }
        }
        """;

        var result = await sut.HandleAsync(payload, new Dictionary<string, string>());

        result.Handled.Should().BeTrue();
        result.DialogueAnswer.Should().NotBeNull();
        result.DialogueAnswer!.Answer.Should().Be("yes");
        result.DialogueAnswer.Comment.Should().BeNull();
    }

    [Fact]
    public void ConversationLookupResult_CarriesFields()
    {
        var result = new ConversationLookupResult("job-123", "pr:org/api#5", "q-456");

        result.JobId.Should().Be("job-123");
        result.ChannelId.Should().Be("pr:org/api#5");
        result.PendingQuestionId.Should().Be("q-456");
    }

    [Fact]
    public void ConversationLookupResult_NullPendingQuestion()
    {
        var result = new ConversationLookupResult("job-123", "pr:org/api#5", null);

        result.PendingQuestionId.Should().BeNull();
    }

    [Fact]
    public async Task GitHubPrComment_FixCommand_TriggerInputContainsPrContext()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 200,
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

        var result = await sut.HandleAsync(payload, new Dictionary<string, string>());

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Contain("pr:org/my-api#42");
        result.DialogueAnswer.Should().BeNull();
    }
}
