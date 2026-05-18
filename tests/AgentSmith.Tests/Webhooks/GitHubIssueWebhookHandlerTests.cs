using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

/// <summary>
/// p0140b: handlers no longer carry routing fields on WebhookResult. They resolve matches
/// via IEnvelopeProjectResolver and hand them to WebhookSpawnDispatcher (which invokes
/// ISpawnPipelineRunsUseCase). These tests use Moq for the resolver + spawn use case and
/// verify Handled + spawn invocation per match.
/// </summary>
public sealed class GitHubIssueWebhookHandlerTests
{
    private const string ConfigPath = "test-config.yml";

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static (GitHubIssueWebhookHandler handler,
                    Mock<IEnvelopeProjectResolver> resolver,
                    Mock<ISpawnPipelineRunsUseCase> spawn)
        CreateHandler(AgentSmithConfig config)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(c => c.LoadConfig(ConfigPath)).Returns(config);
        var resolver = new Mock<IEnvelopeProjectResolver>();
        var spawn = new Mock<ISpawnPipelineRunsUseCase>();
        spawn.Setup(s => s.ExecuteAsync(
                It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(), It.IsAny<string>(),
                It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(),
                It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()))
            .ReturnsAsync(new SpawnResult(Array.Empty<ClaimResult>()));
        var dispatcher = new WebhookSpawnDispatcher(
            spawn.Object,
            new Mock<ITicketProviderFactory>().Object,
            NullLogger<WebhookSpawnDispatcher>.Instance);
        var handler = new GitHubIssueWebhookHandler(
            loader.Object, new ServerContext(ConfigPath),
            resolver.Object, dispatcher,
            NullLogger<GitHubIssueWebhookHandler>.Instance);
        return (handler, resolver, spawn);
    }

    private static AgentSmithConfig BuildConfig() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["my-api"] = new()
            {
                Name = "my-api",
                Repo = new RepoConnection { Name = "my-api", Url = "https://github.com/org/my-api" },
                GithubTrigger = new WebhookTriggerConfig
                {
                    ProjectResolution = new ProjectResolutionConfig
                    {
                        Strategy = ResolutionStrategy.Tag, Value = "agent-smith"
                    },
                    DefaultPipeline = "fix-bug"
                }
            }
        }
    };

    private const string LabeledPayload = """
        {
            "action": "labeled",
            "label": { "name": "agent-smith" },
            "issue": { "number": 42, "state": "open" },
            "repository": { "name": "my-api", "html_url": "https://github.com/org/my-api" }
        }
        """;

    [Fact]
    public void CanHandle_CorrectPlatform()
    {
        var (sut, _, _) = CreateHandler(BuildConfig());
        sut.CanHandle("github", "issues").Should().BeTrue();
        sut.CanHandle("github", "pull_request").Should().BeFalse();
        sut.CanHandle("gitlab", "issues").Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_NotLabeled_ReturnsNotHandled()
    {
        var (sut, _, spawn) = CreateHandler(BuildConfig());
        var payload = """{ "action": "opened", "issue": { "number": 1, "state": "open" }, "repository": { "name": "x", "html_url": "https://github.com/org/x" } }""";

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
        spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(),
            It.IsAny<string>(), It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ZeroMatches_HandledTrueButNoSpawn()
    {
        var (sut, resolver, spawn) = CreateHandler(BuildConfig());
        resolver.Setup(r => r.Resolve(It.IsAny<AgentSmithConfig>(), It.IsAny<IncomingTicketEnvelope>()))
            .Returns(Array.Empty<ProjectMatch>());

        var result = await sut.HandleAsync(LabeledPayload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().BeNull();
        result.ProjectName.Should().BeNull();
        spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(),
            It.IsAny<string>(), It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_OneMatch_DispatchesOneSpawnCall()
    {
        var (sut, resolver, spawn) = CreateHandler(BuildConfig());
        resolver.Setup(r => r.Resolve(It.IsAny<AgentSmithConfig>(), It.IsAny<IncomingTicketEnvelope>()))
            .Returns(new[] { new ProjectMatch("my-api", "fix-bug", "github") });

        var result = await sut.HandleAsync(LabeledPayload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        spawn.Verify(s => s.ExecuteAsync(
            It.IsAny<AgentSmithConfig>(),
            It.Is<ResolvedProject>(p => p.Name == "my-api"),
            "fix-bug",
            It.Is<IncomingTicketEnvelope>(e => e.TicketId == "42" && e.Platform == "github"),
            It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<Dictionary<string, string>?>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_StatusNotInTriggerStatuses_SkipsSpawn()
    {
        var config = BuildConfig();
        config.Projects["my-api"].GithubTrigger!.TriggerStatuses = new List<string> { "closed" };
        var (sut, resolver, spawn) = CreateHandler(config);
        resolver.Setup(r => r.Resolve(It.IsAny<AgentSmithConfig>(), It.IsAny<IncomingTicketEnvelope>()))
            .Returns(new[] { new ProjectMatch("my-api", "fix-bug", "github") });

        var result = await sut.HandleAsync(LabeledPayload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(),
            It.IsAny<string>(), It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Never);
    }
}
