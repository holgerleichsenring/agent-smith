using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Webhooks;

public sealed class AzureDevOpsPrCommentWebhookHandlerTests
{
    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static AzureDevOpsPrCommentWebhookHandler CreateSut() =>
        new(TestCommentIntentParserFactory.Create(),
            TestCommentIntentParserFactory.Context,
            NullLogger<AzureDevOpsPrCommentWebhookHandler>.Instance);

    [Fact]
    public void CanHandle_CorrectEventTypes()
    {
        var sut = CreateSut();

        sut.CanHandle("azuredevops", "ms.vss-code.git-pullrequest-comment-event").Should().BeTrue();
        sut.CanHandle("azuredevops", "workitem.updated").Should().BeFalse();
        sut.CanHandle("github", "ms.vss-code.git-pullrequest-comment-event").Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_FixCommand_ReturnsPipeline()
    {
        var sut = CreateSut();
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
        result.Pipeline.Should().Be("fix-bug");
        result.TriggerInput.Should().Contain("pr:MyProject/my-api#58");
    }

    [Fact]
    public async Task HandleAsync_FixWithTicket_PropagatesTicket()
    {
        var sut = CreateSut();
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
        result.Pipeline.Should().Be("fix-bug");
        result.TriggerInput.Should().Contain("#77");
        result.TriggerInput.Should().Contain("pr:MyProject/my-api#58");
    }

    [Fact]
    public async Task HandleAsync_SecurityScan_ReturnsSecurityPipeline()
    {
        var sut = CreateSut();
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
        result.Pipeline.Should().Be("security-scan");
        result.TriggerInput.Should().Contain("pr:MyProject/my-api#12");
    }

    [Fact]
    public async Task HandleAsync_Approve_ReturnsDialogueAnswer()
    {
        var sut = CreateSut();
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
        var sut = CreateSut();
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
        var sut = CreateSut();
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
