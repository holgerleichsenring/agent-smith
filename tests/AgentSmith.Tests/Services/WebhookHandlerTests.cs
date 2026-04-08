using AgentSmith.Host.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class WebhookHandlerTests
{
    [Fact]
    public async Task GitHubIssue_LabeledAgentSmith_ReturnsHandled()
    {
        var sut = new GitHubIssueWebhookHandler(NullLogger<GitHubIssueWebhookHandler>.Instance);
        var payload = """
        {
            "action": "labeled",
            "label": { "name": "agent-smith" },
            "issue": { "number": 42 },
            "repository": { "name": "my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, new Dictionary<string, string>());

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Be("fix #42 in my-api");
        result.Pipeline.Should().BeNull();
    }

    [Fact]
    public async Task GitHubIssue_WrongLabel_ReturnsNotHandled()
    {
        var sut = new GitHubIssueWebhookHandler(NullLogger<GitHubIssueWebhookHandler>.Instance);
        var payload = """
        {
            "action": "labeled",
            "label": { "name": "bug" },
            "issue": { "number": 42 },
            "repository": { "name": "my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, new Dictionary<string, string>());

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task GitHubIssue_NotLabeled_ReturnsNotHandled()
    {
        var sut = new GitHubIssueWebhookHandler(NullLogger<GitHubIssueWebhookHandler>.Instance);
        var payload = """{ "action": "opened", "issue": { "number": 1 }, "repository": { "name": "x" } }""";

        var result = await sut.HandleAsync(payload, new Dictionary<string, string>());

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public void GitHubIssue_CanHandle_CorrectPlatform()
    {
        var sut = new GitHubIssueWebhookHandler(NullLogger<GitHubIssueWebhookHandler>.Instance);

        sut.CanHandle("github", "issues").Should().BeTrue();
        sut.CanHandle("github", "pull_request").Should().BeFalse();
        sut.CanHandle("gitlab", "issues").Should().BeFalse();
    }

    [Fact]
    public async Task GitHubPr_LabeledSecurityReview_ReturnsSecurityScan()
    {
        var sut = new GitHubPrLabelWebhookHandler(NullLogger<GitHubPrLabelWebhookHandler>.Instance);
        var payload = """
        {
            "action": "labeled",
            "label": { "name": "security-review" },
            "pull_request": { "number": 7 },
            "repository": { "name": "my-api", "clone_url": "https://github.com/org/my-api.git" }
        }
        """;

        var result = await sut.HandleAsync(payload, new Dictionary<string, string>());

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Contain("my-api");
        result.Pipeline.Should().Be("security-scan");
    }

    [Fact]
    public void GitHubPr_CanHandle_CorrectPlatform()
    {
        var sut = new GitHubPrLabelWebhookHandler(NullLogger<GitHubPrLabelWebhookHandler>.Instance);

        sut.CanHandle("github", "pull_request").Should().BeTrue();
        sut.CanHandle("github", "issues").Should().BeFalse();
    }

    [Fact]
    public async Task GitLabMr_LabeledSecurityReview_ReturnsSecurityScan()
    {
        var sut = new GitLabMrLabelWebhookHandler(NullLogger<GitLabMrLabelWebhookHandler>.Instance);
        var payload = """
        {
            "object_attributes": { "action": "update", "iid": 3 },
            "labels": [{ "title": "security-review" }],
            "project": { "path": "my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, new Dictionary<string, string>());

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("security-scan");
    }

    [Fact]
    public async Task GitLabMr_NoMatchingLabel_ReturnsNotHandled()
    {
        var sut = new GitLabMrLabelWebhookHandler(NullLogger<GitLabMrLabelWebhookHandler>.Instance);
        var payload = """
        {
            "object_attributes": { "action": "update", "iid": 3 },
            "labels": [{ "title": "needs-review" }],
            "project": { "path": "my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, new Dictionary<string, string>());

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public void GitLabMr_CanHandle_CorrectPlatform()
    {
        var sut = new GitLabMrLabelWebhookHandler(NullLogger<GitLabMrLabelWebhookHandler>.Instance);

        sut.CanHandle("gitlab", "merge_request").Should().BeTrue();
        sut.CanHandle("gitlab", "push").Should().BeFalse();
    }

    [Fact]
    public async Task AzDO_TaggedSecurityReview_ReturnsSecurityScan()
    {
        var sut = new AzureDevOpsWorkItemWebhookHandler(
            NullLogger<AzureDevOpsWorkItemWebhookHandler>.Instance);
        var payload = """
        {
            "resource": {
                "id": 99,
                "fields": { "System.Tags": "security-review; urgent" }
            },
            "resourceContainers": {
                "project": { "id": "my-project" }
            }
        }
        """;

        var result = await sut.HandleAsync(payload, new Dictionary<string, string>());

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("security-scan");
    }

    [Fact]
    public async Task AzDO_NoTag_ReturnsNotHandled()
    {
        var sut = new AzureDevOpsWorkItemWebhookHandler(
            NullLogger<AzureDevOpsWorkItemWebhookHandler>.Instance);
        var payload = """
        {
            "resource": {
                "id": 99,
                "fields": { "System.Tags": "bug; P1" }
            },
            "resourceContainers": {
                "project": { "id": "my-project" }
            }
        }
        """;

        var result = await sut.HandleAsync(payload, new Dictionary<string, string>());

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public void AzDO_CanHandle_CorrectPlatform()
    {
        var sut = new AzureDevOpsWorkItemWebhookHandler(
            NullLogger<AzureDevOpsWorkItemWebhookHandler>.Instance);

        sut.CanHandle("azuredevops", "workitem.updated").Should().BeTrue();
        sut.CanHandle("azuredevops", "build.complete").Should().BeFalse();
    }

    [Fact]
    public void GitHubPrComment_CanHandle_CorrectEventTypes()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);

        sut.CanHandle("github", "issue_comment").Should().BeTrue();
        sut.CanHandle("github", "pull_request_review_comment").Should().BeTrue();
        sut.CanHandle("github", "issues").Should().BeFalse();
        sut.CanHandle("gitlab", "issue_comment").Should().BeFalse();
    }

    [Fact]
    public async Task GitHubPrComment_FixCommand_ReturnsPipeline()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 100,
                "body": "/agent-smith fix",
                "user": { "login": "dev-user" }
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
        result.TriggerInput.Should().Be("fix-bug pr:org/my-api#42");
        result.Pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public async Task GitHubPrComment_FixWithArguments_ReturnsArguments()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 101,
                "body": "/agent-smith fix #123 in my-api",
                "user": { "login": "dev-user" }
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
        result.TriggerInput.Should().Be("fix-bug #123 in my-api");
        result.Pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public async Task GitHubPrComment_SecurityScan_ReturnsSecurityPipeline()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 102,
                "body": "/agent-smith security-scan",
                "user": { "login": "dev-user" }
            },
            "issue": {
                "number": 7,
                "pull_request": { "url": "https://api.github.com/repos/org/my-api/pulls/7" }
            },
            "repository": { "full_name": "org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, new Dictionary<string, string>());

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Be("security-scan pr:org/my-api#7");
        result.Pipeline.Should().Be("security-scan");
    }

    [Fact]
    public async Task GitHubPrComment_Help_ReturnsNotHandled()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 103,
                "body": "/agent-smith help",
                "user": { "login": "dev-user" }
            },
            "issue": {
                "number": 42,
                "pull_request": { "url": "https://api.github.com/repos/org/my-api/pulls/42" }
            },
            "repository": { "full_name": "org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, new Dictionary<string, string>());

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task GitHubPrComment_Approve_ReturnsHandledWithDialogueAnswer()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 104,
                "body": "/approve looks good",
                "user": { "login": "dev-user" }
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
        result.DialogueAnswer.Should().NotBeNull();
        result.DialogueAnswer!.Answer.Should().Be("yes");
    }

    [Fact]
    public async Task GitHubPrComment_PlainIssueComment_ReturnsNotHandled()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 105,
                "body": "/agent-smith fix",
                "user": { "login": "dev-user" }
            },
            "issue": {
                "number": 42
            },
            "repository": { "full_name": "org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, new Dictionary<string, string>());

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task GitHubPrComment_EditedAction_ReturnsNotHandled()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "edited",
            "comment": {
                "id": 106,
                "body": "/agent-smith fix",
                "user": { "login": "dev-user" }
            },
            "issue": {
                "number": 42,
                "pull_request": { "url": "https://api.github.com/repos/org/my-api/pulls/42" }
            },
            "repository": { "full_name": "org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, new Dictionary<string, string>());

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task GitHubPrComment_ReviewComment_ReturnsHandled()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 107,
                "body": "/as fix",
                "user": { "login": "dev-user" }
            },
            "pull_request": { "number": 10 },
            "repository": { "full_name": "org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, new Dictionary<string, string>());

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Be("fix-bug pr:org/my-api#10");
        result.Pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public async Task GitHubPrComment_UnknownCommand_ReturnsNotHandled()
    {
        var sut = new GitHubPrCommentWebhookHandler(
            NullLogger<GitHubPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "action": "created",
            "comment": {
                "id": 108,
                "body": "Just a regular comment",
                "user": { "login": "dev-user" }
            },
            "issue": {
                "number": 42,
                "pull_request": { "url": "https://api.github.com/repos/org/my-api/pulls/42" }
            },
            "repository": { "full_name": "org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, new Dictionary<string, string>());

        result.Handled.Should().BeFalse();
    }
}
