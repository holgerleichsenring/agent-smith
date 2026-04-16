using AgentSmith.Cli.Services.Webhooks;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class WebhookHandlerTests
{
    private const string ConfigPath = "test-config.yml";

    private static AgentSmithConfig BuildGitHubConfig(string repoUrl = "https://github.com/org/my-api") =>
        new()
        {
            Projects = new Dictionary<string, ProjectConfig>
            {
                ["my-api"] = new()
                {
                    Source = new SourceConfig { Url = repoUrl }
                }
            }
        };

    private static AgentSmithConfig BuildAzDoConfig() =>
        new()
        {
            Projects = new Dictionary<string, ProjectConfig>
            {
                ["my-project"] = new()
                {
                    Tickets = new TicketConfig { Type = "AzureDevOps" }
                }
            }
        };

    private static AgentSmithConfig BuildGitLabConfig(string repoUrl = "https://gitlab.com/org/my-api") =>
        new()
        {
            Projects = new Dictionary<string, ProjectConfig>
            {
                ["my-api"] = new()
                {
                    Source = new SourceConfig { Url = repoUrl }
                }
            }
        };

    private static GitHubIssueWebhookHandler CreateGitHubHandler(AgentSmithConfig? config = null)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(c => c.LoadConfig(ConfigPath)).Returns(config ?? BuildGitHubConfig());
        return new GitHubIssueWebhookHandler(loader.Object, new ServerContext(ConfigPath),
            NullLogger<GitHubIssueWebhookHandler>.Instance);
    }

    private static GitLabMrLabelWebhookHandler CreateGitLabHandler(AgentSmithConfig? config = null)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(c => c.LoadConfig(ConfigPath)).Returns(config ?? BuildGitLabConfig());
        return new GitLabMrLabelWebhookHandler(loader.Object, new ServerContext(ConfigPath),
            NullLogger<GitLabMrLabelWebhookHandler>.Instance);
    }

    private static AzureDevOpsWorkItemWebhookHandler CreateAzDoHandler(AgentSmithConfig? config = null)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(c => c.LoadConfig(ConfigPath)).Returns(config ?? BuildAzDoConfig());
        return new AzureDevOpsWorkItemWebhookHandler(loader.Object, new ServerContext(ConfigPath),
            NullLogger<AzureDevOpsWorkItemWebhookHandler>.Instance);
    }

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // --- GitHub Issue ---

    [Fact]
    public async Task GitHubIssue_LabeledAgentSmith_ReturnsHandled()
    {
        var sut = CreateGitHubHandler();
        var payload = """
        {
            "action": "labeled",
            "label": { "name": "agent-smith" },
            "issue": { "number": 42 },
            "repository": { "name": "my-api", "html_url": "https://github.com/org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.ProjectName.Should().Be("my-api");
        result.TicketId.Should().Be("42");
        result.TriggerInput.Should().BeNull();
    }

    [Fact]
    public async Task GitHubIssue_WrongLabel_ReturnsNotHandled()
    {
        var sut = CreateGitHubHandler();
        var payload = """
        {
            "action": "labeled",
            "label": { "name": "bug" },
            "issue": { "number": 42 },
            "repository": { "name": "my-api", "html_url": "https://github.com/org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task GitHubIssue_NotLabeled_ReturnsNotHandled()
    {
        var sut = CreateGitHubHandler();
        var payload = """{ "action": "opened", "issue": { "number": 1 }, "repository": { "name": "x", "html_url": "https://github.com/org/x" } }""";

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public void GitHubIssue_CanHandle_CorrectPlatform()
    {
        var sut = CreateGitHubHandler();

        sut.CanHandle("github", "issues").Should().BeTrue();
        sut.CanHandle("github", "pull_request").Should().BeFalse();
        sut.CanHandle("gitlab", "issues").Should().BeFalse();
    }

    // --- GitHub PR Label ---

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

        var result = await sut.HandleAsync(payload, EmptyHeaders);

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

    // --- GitLab MR Label ---

    [Fact]
    public async Task GitLabMr_LabeledSecurityReview_ReturnsSecurityScan()
    {
        var sut = CreateGitLabHandler();
        var payload = """
        {
            "object_attributes": { "action": "update", "iid": 3 },
            "labels": [{ "title": "security-review" }],
            "project": { "path": "my-api", "web_url": "https://gitlab.com/org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("security-scan");
        result.ProjectName.Should().Be("my-api");
        result.TicketId.Should().Be("3");
    }

    [Fact]
    public async Task GitLabMr_NoMatchingLabel_ReturnsNotHandled()
    {
        var sut = CreateGitLabHandler();
        var payload = """
        {
            "object_attributes": { "action": "update", "iid": 3 },
            "labels": [{ "title": "needs-review" }],
            "project": { "path": "my-api", "web_url": "https://gitlab.com/org/my-api" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public void GitLabMr_CanHandle_CorrectPlatform()
    {
        var sut = CreateGitLabHandler();

        sut.CanHandle("gitlab", "merge_request").Should().BeTrue();
        sut.CanHandle("gitlab", "push").Should().BeFalse();
    }

    // --- Azure DevOps Work Item ---

    [Fact]
    public async Task AzDO_TaggedSecurityReview_ReturnsSecurityScan()
    {
        var sut = CreateAzDoHandler();
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

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("security-scan");
        result.ProjectName.Should().Be("my-project");
        result.TicketId.Should().Be("99");
    }

    [Fact]
    public async Task AzDO_NoTag_ReturnsNotHandled()
    {
        var sut = CreateAzDoHandler();
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

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public void AzDO_CanHandle_CorrectPlatform()
    {
        var sut = CreateAzDoHandler();

        sut.CanHandle("azuredevops", "workitem.updated").Should().BeTrue();
        sut.CanHandle("azuredevops", "build.complete").Should().BeFalse();
    }

    // --- GitHub PR Comment (unchanged — still uses TriggerInput) ---

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

    // --- GitLab MR Comment Webhook Handler Tests (p59b) ---

    [Fact]
    public void GitLabMrComment_CanHandle_CorrectEventTypes()
    {
        var sut = new GitLabMrCommentWebhookHandler(
            NullLogger<GitLabMrCommentWebhookHandler>.Instance);

        sut.CanHandle("gitlab", "note hook").Should().BeTrue();
        sut.CanHandle("gitlab", "merge_request").Should().BeFalse();
        sut.CanHandle("github", "note hook").Should().BeFalse();
    }

    [Fact]
    public async Task GitLabMrComment_FixCommand_ReturnsPipeline()
    {
        var sut = new GitLabMrCommentWebhookHandler(
            NullLogger<GitLabMrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "object_kind": "note",
            "user": { "username": "dev-user" },
            "project": { "path_with_namespace": "org/my-api" },
            "object_attributes": {
                "id": 200,
                "note": "/agent-smith fix",
                "noteable_type": "MergeRequest"
            },
            "merge_request": { "iid": 15 }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Be("fix-bug mr:org/my-api!15");
        result.Pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public async Task GitLabMrComment_FixWithArguments_ReturnsArguments()
    {
        var sut = new GitLabMrCommentWebhookHandler(
            NullLogger<GitLabMrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "object_kind": "note",
            "user": { "username": "dev-user" },
            "project": { "path_with_namespace": "org/my-api" },
            "object_attributes": {
                "id": 201,
                "note": "/agent-smith fix #99 in core",
                "noteable_type": "MergeRequest"
            },
            "merge_request": { "iid": 15 }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Be("fix-bug #99 in core");
        result.Pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public async Task GitLabMrComment_SecurityScan_ReturnsSecurityPipeline()
    {
        var sut = new GitLabMrCommentWebhookHandler(
            NullLogger<GitLabMrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "object_kind": "note",
            "user": { "username": "dev-user" },
            "project": { "path_with_namespace": "org/my-api" },
            "object_attributes": {
                "id": 202,
                "note": "/agent-smith security-scan",
                "noteable_type": "MergeRequest"
            },
            "merge_request": { "iid": 3 }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Be("security-scan mr:org/my-api!3");
        result.Pipeline.Should().Be("security-scan");
    }

    [Fact]
    public async Task GitLabMrComment_Approve_ReturnsDialogueAnswer()
    {
        var sut = new GitLabMrCommentWebhookHandler(
            NullLogger<GitLabMrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "object_kind": "note",
            "user": { "username": "dev-user" },
            "project": { "path_with_namespace": "org/my-api" },
            "object_attributes": {
                "id": 203,
                "note": "/approve looks good",
                "noteable_type": "MergeRequest"
            },
            "merge_request": { "iid": 15 }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.DialogueAnswer.Should().NotBeNull();
        result.DialogueAnswer!.Platform.Should().Be("gitlab");
        result.DialogueAnswer.Answer.Should().Be("yes");
        result.DialogueAnswer.PrIdentifier.Should().Be("15");
    }

    [Fact]
    public async Task GitLabMrComment_NoteOnIssue_ReturnsNotHandled()
    {
        var sut = new GitLabMrCommentWebhookHandler(
            NullLogger<GitLabMrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "object_kind": "note",
            "user": { "username": "dev-user" },
            "project": { "path_with_namespace": "org/my-api" },
            "object_attributes": {
                "id": 204,
                "note": "/agent-smith fix",
                "noteable_type": "Issue"
            }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task GitLabMrComment_Help_ReturnsNotHandled()
    {
        var sut = new GitLabMrCommentWebhookHandler(
            NullLogger<GitLabMrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "object_kind": "note",
            "user": { "username": "dev-user" },
            "project": { "path_with_namespace": "org/my-api" },
            "object_attributes": {
                "id": 205,
                "note": "/agent-smith help",
                "noteable_type": "MergeRequest"
            },
            "merge_request": { "iid": 15 }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task GitLabMrComment_RegularComment_ReturnsNotHandled()
    {
        var sut = new GitLabMrCommentWebhookHandler(
            NullLogger<GitLabMrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "object_kind": "note",
            "user": { "username": "dev-user" },
            "project": { "path_with_namespace": "org/my-api" },
            "object_attributes": {
                "id": 206,
                "note": "Just a regular comment",
                "noteable_type": "MergeRequest"
            },
            "merge_request": { "iid": 15 }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    // --- Azure DevOps PR Comment Webhook Handler Tests (p59c) ---

    [Fact]
    public void AzureDevOpsPrComment_CanHandle_CorrectEventTypes()
    {
        var sut = new AzureDevOpsPrCommentWebhookHandler(
            NullLogger<AzureDevOpsPrCommentWebhookHandler>.Instance);

        sut.CanHandle("azuredevops", "ms.vss-code.git-pullrequest-comment-event").Should().BeTrue();
        sut.CanHandle("azuredevops", "workitem.updated").Should().BeFalse();
        sut.CanHandle("github", "ms.vss-code.git-pullrequest-comment-event").Should().BeFalse();
    }

    [Fact]
    public async Task AzureDevOpsPrComment_FixCommand_ReturnsPipeline()
    {
        var sut = new AzureDevOpsPrCommentWebhookHandler(
            NullLogger<AzureDevOpsPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "eventType": "ms.vss-code.git-pullrequest-comment-event",
            "resource": {
                "comment": {
                    "id": 300,
                    "content": "/agent-smith fix",
                    "author": { "uniqueName": "dev@org.com" }
                },
                "pullRequest": {
                    "pullRequestId": 58,
                    "repository": {
                        "name": "my-api",
                        "project": { "name": "MyProject" }
                    }
                }
            }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Be("fix-bug pr:MyProject/my-api#58");
        result.Pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public async Task AzureDevOpsPrComment_FixWithArguments_ReturnsArguments()
    {
        var sut = new AzureDevOpsPrCommentWebhookHandler(
            NullLogger<AzureDevOpsPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "eventType": "ms.vss-code.git-pullrequest-comment-event",
            "resource": {
                "comment": {
                    "id": 301,
                    "content": "/agent-smith fix #77 in payments",
                    "author": { "uniqueName": "dev@org.com" }
                },
                "pullRequest": {
                    "pullRequestId": 58,
                    "repository": {
                        "name": "my-api",
                        "project": { "name": "MyProject" }
                    }
                }
            }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Be("fix-bug #77 in payments");
        result.Pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public async Task AzureDevOpsPrComment_SecurityScan_ReturnsSecurityPipeline()
    {
        var sut = new AzureDevOpsPrCommentWebhookHandler(
            NullLogger<AzureDevOpsPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "eventType": "ms.vss-code.git-pullrequest-comment-event",
            "resource": {
                "comment": {
                    "id": 302,
                    "content": "/agent-smith security-scan",
                    "author": { "uniqueName": "dev@org.com" }
                },
                "pullRequest": {
                    "pullRequestId": 12,
                    "repository": {
                        "name": "my-api",
                        "project": { "name": "MyProject" }
                    }
                }
            }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Be("security-scan pr:MyProject/my-api#12");
        result.Pipeline.Should().Be("security-scan");
    }

    [Fact]
    public async Task AzureDevOpsPrComment_Approve_ReturnsDialogueAnswer()
    {
        var sut = new AzureDevOpsPrCommentWebhookHandler(
            NullLogger<AzureDevOpsPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "eventType": "ms.vss-code.git-pullrequest-comment-event",
            "resource": {
                "comment": {
                    "id": 303,
                    "content": "/approve ship it",
                    "author": { "uniqueName": "dev@org.com" }
                },
                "pullRequest": {
                    "pullRequestId": 58,
                    "repository": {
                        "name": "my-api",
                        "project": { "name": "MyProject" }
                    }
                }
            }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.DialogueAnswer.Should().NotBeNull();
        result.DialogueAnswer!.Platform.Should().Be("azuredevops");
        result.DialogueAnswer.Answer.Should().Be("yes");
        result.DialogueAnswer.PrIdentifier.Should().Be("58");
    }

    [Fact]
    public async Task AzureDevOpsPrComment_Help_ReturnsNotHandled()
    {
        var sut = new AzureDevOpsPrCommentWebhookHandler(
            NullLogger<AzureDevOpsPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "eventType": "ms.vss-code.git-pullrequest-comment-event",
            "resource": {
                "comment": {
                    "id": 304,
                    "content": "/agent-smith help",
                    "author": { "uniqueName": "dev@org.com" }
                },
                "pullRequest": {
                    "pullRequestId": 58,
                    "repository": {
                        "name": "my-api",
                        "project": { "name": "MyProject" }
                    }
                }
            }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task AzureDevOpsPrComment_RegularComment_ReturnsNotHandled()
    {
        var sut = new AzureDevOpsPrCommentWebhookHandler(
            NullLogger<AzureDevOpsPrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "eventType": "ms.vss-code.git-pullrequest-comment-event",
            "resource": {
                "comment": {
                    "id": 305,
                    "content": "Just a regular comment",
                    "author": { "uniqueName": "dev@org.com" }
                },
                "pullRequest": {
                    "pullRequestId": 58,
                    "repository": {
                        "name": "my-api",
                        "project": { "name": "MyProject" }
                    }
                }
            }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }
}
