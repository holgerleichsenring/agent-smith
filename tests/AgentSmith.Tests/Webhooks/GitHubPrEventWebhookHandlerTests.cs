using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

public sealed class GitHubPrEventWebhookHandlerTests
{
    private const string ConfigPath = "test-config.yml";

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static GitHubPrEventWebhookHandler CreateHandler(AgentSmithConfig? config = null)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(c => c.LoadConfig(ConfigPath)).Returns(config ?? BuildConfig());
        return new GitHubPrEventWebhookHandler(
            loader.Object, new ServerContext(ConfigPath), new PrReviewRouteResolver(),
            NullLogger<GitHubPrEventWebhookHandler>.Instance);
    }

    private static AgentSmithConfig BuildConfig(
        Dictionary<string, string>? pipelineFromLabel = null) => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["my-api"] = new()
            {
                Repos = [new RepoConnection { Name = "primary", Url = "https://github.com/org/my-api" }],
                GithubTrigger = pipelineFromLabel is null
                    ? null
                    : new WebhookTriggerConfig { PipelineFromLabel = pipelineFromLabel },
            },
        },
    };

    private static string Payload(string action, string label = "") => $$"""
    {
        "action": "{{action}}",
        "pull_request": {
            "number": 42,
            "head": { "sha": "headsha123", "ref": "feature/x" },
            "base": { "sha": "basesha456" },
            "user": { "login": "dev-a" },
            "labels": [{{label}}]
        },
        "repository": {
            "full_name": "org/my-api",
            "clone_url": "https://github.com/org/my-api.git"
        }
    }
    """;

    [Fact]
    public async Task GitHubPrEventWebhookHandler_PrOpenedPayload_EnqueuesPrReviewRun()
    {
        var result = await CreateHandler().HandleAsync(Payload("opened"), EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("pr-review");
        result.TriggerInput.Should().Contain("pr-review").And.Contain("my-api").And.Contain("org/my-api#42");
    }

    [Fact]
    public async Task GitHubPrEventWebhookHandler_PrSynchronizePayload_EnqueuesPrReviewRun()
    {
        var result = await CreateHandler().HandleAsync(Payload("synchronize"), EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("pr-review");
    }

    [Fact]
    public async Task HandleAsync_SeedsPrCoordinatesIntoInitialContext()
    {
        var result = await CreateHandler().HandleAsync(Payload("opened"), EmptyHeaders);

        result.InitialContext.Should().NotBeNull();
        result.InitialContext![ContextKeys.PrNumber].Should().Be("42");
        result.InitialContext[ContextKeys.PrHead].Should().Be("headsha123");
        result.InitialContext[ContextKeys.PrBase].Should().Be("basesha456");
        result.InitialContext[ContextKeys.PrAuthor].Should().Be("dev-a");
        result.InitialContext[ContextKeys.CheckoutBranch].Should().Be("feature/x");
        result.InitialContext[ContextKeys.SourceOverrideRepo].Should().Be("primary");
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("labeled")]
    [InlineData("edited")]
    public async Task HandleAsync_NonTriggerAction_ReturnsNotHandled(string action)
    {
        var result = await CreateHandler().HandleAsync(Payload(action), EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_RepoNotConfigured_ReturnsNotHandledWithReason()
    {
        var config = BuildConfig();
        config.Projects["my-api"] = new ResolvedProject
        {
            Repos = [new RepoConnection { Name = "primary", Url = "https://github.com/org/other-repo" }],
        };

        var result = await CreateHandler(config).HandleAsync(Payload("opened"), EmptyHeaders);

        result.Handled.Should().BeFalse();
        result.SkipReason.Should().Contain("org/my-api");
    }

    [Fact]
    public async Task HandleAsync_PrLabelMappedInPipelineFromLabel_OverridesDefaultRoute()
    {
        // The operator's opt-out lever: a mapped PR label routes away from pr-review.
        var config = BuildConfig(new Dictionary<string, string> { ["security"] = "security-scan" });

        var result = await CreateHandler(config).HandleAsync(
            Payload("opened", label: """{ "name": "security" }"""), EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("security-scan");
    }

    [Fact]
    public void CanHandle_OnlyGitHubPullRequestEvents()
    {
        var sut = CreateHandler();

        sut.CanHandle("github", "pull_request").Should().BeTrue();
        sut.CanHandle("github", "issues").Should().BeFalse();
        sut.CanHandle("gitlab", "pull_request").Should().BeFalse();
    }
}
