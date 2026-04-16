using AgentSmith.Cli.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Webhooks;

public sealed class AzureDevOpsPrCommentWebhookHandlerTests
{
    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void CanHandle_CorrectEventTypes()
    {
        var sut = new AzureDevOpsPrCommentWebhookHandler(
            NullLogger<AzureDevOpsPrCommentWebhookHandler>.Instance);

        sut.CanHandle("azuredevops", "ms.vss-code.git-pullrequest-comment-event").Should().BeTrue();
        sut.CanHandle("azuredevops", "workitem.updated").Should().BeFalse();
        sut.CanHandle("github", "ms.vss-code.git-pullrequest-comment-event").Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_FixCommand_ReturnsPipeline()
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
    public async Task HandleAsync_FixWithArguments_ReturnsArguments()
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
    public async Task HandleAsync_SecurityScan_ReturnsSecurityPipeline()
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
    public async Task HandleAsync_Approve_ReturnsDialogueAnswer()
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
    public async Task HandleAsync_Help_ReturnsNotHandled()
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
    public async Task HandleAsync_RegularComment_ReturnsNotHandled()
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
