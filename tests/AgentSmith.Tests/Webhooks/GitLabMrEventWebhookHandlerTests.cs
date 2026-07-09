using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

public sealed class GitLabMrEventWebhookHandlerTests
{
    private const string ConfigPath = "test-config.yml";

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static GitLabMrEventWebhookHandler CreateHandler()
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["my-api"] = new()
                {
                    Repos = [new RepoConnection { Name = "primary", Url = "https://gitlab.com/org/my-api" }],
                },
            },
        };
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(c => c.LoadConfig(ConfigPath)).Returns(config);
        return new GitLabMrEventWebhookHandler(
            loader.Object, new ServerContext(ConfigPath), new PrReviewRouteResolver(),
            NullLogger<GitLabMrEventWebhookHandler>.Instance);
    }

    private static string Payload(string action, string extraAttrs = "") => $$"""
    {
        "user": { "username": "dev-a" },
        "object_attributes": {
            "iid": 7,
            "action": "{{action}}",
            "source_branch": "feature/x",
            "last_commit": { "id": "headsha123" }{{extraAttrs}}
        },
        "labels": [],
        "project": {
            "path_with_namespace": "org/my-api",
            "web_url": "https://gitlab.com/org/my-api"
        }
    }
    """;

    [Fact]
    public async Task HandleAsync_MrOpened_EnqueuesPrReviewRun()
    {
        var result = await CreateHandler().HandleAsync(Payload("open"), EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("pr-review");
        result.InitialContext![ContextKeys.PrNumber].Should().Be("7");
        result.InitialContext[ContextKeys.CheckoutBranch].Should().Be("feature/x");
        result.InitialContext[ContextKeys.PrHead].Should().Be("headsha123");
    }

    [Fact]
    public async Task HandleAsync_UpdateWithOldrev_IsSynchronize_EnqueuesPrReviewRun()
    {
        var result = await CreateHandler().HandleAsync(
            Payload("update", extraAttrs: """, "oldrev": "prevsha" """), EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("pr-review");
    }

    [Fact]
    public async Task HandleAsync_UpdateWithoutOldrev_LabelOrTitleEdit_ReturnsNotHandled()
    {
        var result = await CreateHandler().HandleAsync(Payload("update"), EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public void CanHandle_OnlyGitLabMergeRequestEvents()
    {
        var sut = CreateHandler();

        sut.CanHandle("gitlab", "merge_request").Should().BeTrue();
        sut.CanHandle("gitlab", "issue").Should().BeFalse();
    }
}
