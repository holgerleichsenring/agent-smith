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

public sealed class AzureDevOpsWorkItemWebhookHandlerTests
{
    private const string ConfigPath = "test-config.yml";

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static (AzureDevOpsWorkItemWebhookHandler handler,
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
        var handler = new AzureDevOpsWorkItemWebhookHandler(
            loader.Object, new ServerContext(ConfigPath),
            resolver.Object, dispatcher,
            NullLogger<AzureDevOpsWorkItemWebhookHandler>.Instance);
        return (handler, resolver, spawn);
    }

    private static AgentSmithConfig BuildConfig() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["my-project"] = new()
            {
                Name = "my-project",
                Tracker = new TrackerConnection { Type = TrackerType.AzureDevOps },
                Repos = new[] { new RepoConnection { Name = "my-project" } },
                AzuredevopsTrigger = new WebhookTriggerConfig
                {
                    ProjectResolution = new ProjectResolutionConfig
                    {
                        Strategy = ResolutionStrategy.Tag, Value = "security-review"
                    },
                    PipelineFromLabel = new Dictionary<string, string>
                    {
                        ["security-review"] = "security-scan"
                    }
                }
            }
        }
    };

    private const string Payload = """
        {
            "resource": {
                "id": 99,
                "fields": { "System.Tags": "security-review; urgent", "System.State": "New" }
            }
        }
        """;

    [Fact]
    public void CanHandle_CorrectPlatform()
    {
        var (sut, _, _) = CreateHandler(BuildConfig());
        sut.CanHandle("azuredevops", "workitem.updated").Should().BeTrue();
        sut.CanHandle("azuredevops", "build.complete").Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_ZeroMatches_HandledNoSpawn()
    {
        var (sut, resolver, spawn) = CreateHandler(BuildConfig());
        resolver.Setup(r => r.Resolve(It.IsAny<AgentSmithConfig>(), It.IsAny<IncomingTicketEnvelope>()))
            .Returns(Array.Empty<ProjectMatch>());

        var result = await sut.HandleAsync(Payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(),
            It.IsAny<string>(), It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_OneMatch_DispatchesOneSpawnCall()
    {
        var (sut, resolver, spawn) = CreateHandler(BuildConfig());
        resolver.Setup(r => r.Resolve(It.IsAny<AgentSmithConfig>(), It.IsAny<IncomingTicketEnvelope>()))
            .Returns(new[] { new ProjectMatch("my-project", "security-scan", "azuredevops") });

        var result = await sut.HandleAsync(Payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        spawn.Verify(s => s.ExecuteAsync(
            It.IsAny<AgentSmithConfig>(),
            It.Is<ResolvedProject>(p => p.Name == "my-project"),
            "security-scan",
            It.Is<IncomingTicketEnvelope>(e => e.TicketId == "99" && e.Platform == "azuredevops"),
            It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<Dictionary<string, string>?>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_StatusFiltered_SkipsSpawn()
    {
        var config = BuildConfig();
        config.Projects["my-project"].AzuredevopsTrigger!.TriggerStatuses = new List<string> { "Active" };
        var (sut, resolver, spawn) = CreateHandler(config);
        resolver.Setup(r => r.Resolve(It.IsAny<AgentSmithConfig>(), It.IsAny<IncomingTicketEnvelope>()))
            .Returns(new[] { new ProjectMatch("my-project", "security-scan", "azuredevops") });

        var result = await sut.HandleAsync(Payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(),
            It.IsAny<string>(), It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Never);
    }
}
