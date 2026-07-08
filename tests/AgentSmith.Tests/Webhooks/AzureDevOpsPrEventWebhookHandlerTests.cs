using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

public sealed class AzureDevOpsPrEventWebhookHandlerTests
{
    private const string ConfigPath = "test-config.yml";

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static AzureDevOpsPrEventWebhookHandler CreateHandler()
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["my-api"] = new()
                {
                    Repos =
                    [
                        new RepoConnection
                        {
                            Name = "primary",
                            Url = "https://dev.azure.com/org/proj/_git/my-api",
                        },
                    ],
                },
            },
        };
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(c => c.LoadConfig(ConfigPath)).Returns(config);
        return new AzureDevOpsPrEventWebhookHandler(
            loader.Object, new ServerContext(ConfigPath), new PrReviewRouteResolver(),
            NullLogger<AzureDevOpsPrEventWebhookHandler>.Instance);
    }

    private static string Payload(string status = "active") => $$"""
    {
        "resource": {
            "pullRequestId": 11,
            "status": "{{status}}",
            "sourceRefName": "refs/heads/feature/x",
            "createdBy": { "uniqueName": "dev-a@example.test" },
            "lastMergeSourceCommit": { "commitId": "headsha123" },
            "lastMergeTargetCommit": { "commitId": "basesha456" },
            "repository": {
                "name": "my-api",
                "remoteUrl": "https://org@dev.azure.com/org/proj/_git/my-api"
            }
        }
    }
    """;

    [Theory]
    [InlineData("git.pullrequest.created")]
    [InlineData("git.pullrequest.updated")]
    public async Task HandleAsync_ActivePrEvent_EnqueuesPrReviewRun(string eventType)
    {
        var sut = CreateHandler();
        sut.CanHandle("azuredevops", eventType).Should().BeTrue();

        var result = await sut.HandleAsync(Payload(), EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("pr-review");
        result.InitialContext![ContextKeys.PrNumber].Should().Be("11");
        result.InitialContext[ContextKeys.CheckoutBranch].Should().Be("feature/x");
        result.InitialContext[ContextKeys.PrHead].Should().Be("headsha123");
        result.InitialContext[ContextKeys.PrBase].Should().Be("basesha456");
    }

    [Fact]
    public async Task HandleAsync_CompletedPr_ReturnsNotHandled()
    {
        var result = await CreateHandler().HandleAsync(Payload(status: "completed"), EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public void CanHandle_OnlyPullRequestCreatedAndUpdated()
    {
        var sut = CreateHandler();

        sut.CanHandle("azuredevops", "git.pullrequest.created").Should().BeTrue();
        sut.CanHandle("azuredevops", "git.pullrequest.updated").Should().BeTrue();
        sut.CanHandle("azuredevops", "ms.vss-code.git-pullrequest-comment-event").Should().BeFalse();
        sut.CanHandle("azuredevops", "workitem.updated").Should().BeFalse();
    }
}
