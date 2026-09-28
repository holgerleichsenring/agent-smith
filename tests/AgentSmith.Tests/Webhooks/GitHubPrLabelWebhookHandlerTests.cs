using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

public sealed class GitHubPrLabelWebhookHandlerTests
{
    private const string ConfigPath = "test-config.yml";

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private const string LabeledPayload = """
        {
            "action": "labeled",
            "label": { "name": "security-review" },
            "pull_request": {
                "number": 7,
                "head": { "sha": "headsha123", "ref": "feature/x" },
                "base": { "sha": "basesha456", "ref": "main" },
                "user": { "login": "alice" }
            },
            "repository": { "name": "my-api", "full_name": "org/my-api", "clone_url": "https://github.com/org/my-api.git" }
        }
        """;

    private static GitHubPrLabelWebhookHandler CreateHandler(AgentSmithConfig config)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(c => c.LoadConfig(ConfigPath)).Returns(config);
        return new GitHubPrLabelWebhookHandler(
            loader.Object, new ServerContext(ConfigPath), new PrTriggerLabelResolver(),
            new PrReviewRouteResolver(new ConfiguredRepoFinder()), new PrRunContextFactory(),
            NullLogger<GitHubPrLabelWebhookHandler>.Instance);
    }

    private static AgentSmithConfig Configured() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["my-api"] = new()
            {
                Repos = new[] { new RepoConnection { Name = "api-repo", Url = "https://github.com/org/my-api" } },
            },
        },
    };

    [Fact]
    public async Task GitHubPrLabel_Labeled_SeedsPrNumberHeadBranchAndRepo()
    {
        var result = await CreateHandler(Configured()).HandleAsync(LabeledPayload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("security-scan");
        result.InitialContext.Should().NotBeNull();
        result.InitialContext![ContextKeys.PrNumber].Should().Be("7");
        result.InitialContext[ContextKeys.CheckoutBranch].Should().Be("feature/x");
        result.InitialContext[ContextKeys.PrHead].Should().Be("headsha123");
        result.InitialContext[ContextKeys.PrBase].Should().Be("basesha456");
        result.InitialContext[ContextKeys.SourceOverrideRepo].Should().Be("api-repo");
    }

    [Fact]
    public async Task GitHubPrLabel_Labeled_NamesOwningProject()
    {
        var result = await CreateHandler(Configured()).HandleAsync(LabeledPayload, EmptyHeaders);

        result.ProjectName.Should().Be("my-api");
        result.TriggerInput.Should().Be("security-scan my-api pr:org/my-api#7");
    }

    [Fact]
    public async Task GitHubPrLabel_UnconfiguredRepo_IsNotHandledWithTheReason()
    {
        var result = await CreateHandler(new AgentSmithConfig()).HandleAsync(LabeledPayload, EmptyHeaders);

        result.Handled.Should().BeFalse();
        result.SkipReason.Should().Contain("org/my-api");
    }

    [Fact]
    public void CanHandle_CorrectPlatform()
    {
        var sut = CreateHandler(Configured());

        sut.CanHandle("github", "pull_request").Should().BeTrue();
        sut.CanHandle("github", "issues").Should().BeFalse();
    }
}
