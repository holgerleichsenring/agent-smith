using System.Text.Json;
using AgentSmith.Application.Services.Triage;
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

public sealed class JiraCommentWebhookHandlerTests
{
    private const string ConfigPath = "test-config.yml";

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static string BuildPayload(
        string commentText = "@agent-smith fix this",
        string issueKey = "PROJ-456",
        string issueStatus = "Open",
        string[]? labels = null)
    {
        var labelArray = labels ?? Array.Empty<string>();
        var labelsJson = JsonSerializer.Serialize(labelArray);
        return $$"""
        {
          "webhookEvent": "jira:comment_created",
          "issue": {
            "key": "{{issueKey}}",
            "fields": {
              "status": { "name": "{{issueStatus}}" },
              "labels": {{labelsJson}}
            }
          },
          "comment": {
            "body": "{{commentText}}",
            "author": { "displayName": "Alice Example" }
          }
        }
        """;
    }

    private static AgentSmithConfig BuildConfig(
        string? commentKeyword = "@agent-smith",
        List<string>? triggerStatuses = null)
    {
        return new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["my-project"] = new()
                {
                    Name = "my-project",
                    Repo = new RepoConnection { Name = "my-project" },
                    JiraTrigger = new JiraTriggerConfig
                    {
                        CommentKeyword = commentKeyword,
                        ProjectResolution = new ProjectResolutionConfig
                        {
                            Strategy = ResolutionStrategy.Tag, Value = "agent-smith"
                        },
                        TriggerStatuses = triggerStatuses ?? new List<string> { "Open" },
                        DoneStatus = "In Review",
                        DefaultPipeline = "fix-bug"
                    }
                }
            }
        };
    }

    private static (JiraCommentWebhookHandler handler,
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
        var handler = new JiraCommentWebhookHandler(
            loader.Object, new ServerContext(ConfigPath),
            resolver.Object, dispatcher,
            new PlanAnswerParser(NullLogger<PlanAnswerParser>.Instance),
            NullLogger<JiraCommentWebhookHandler>.Instance);
        return (handler, resolver, spawn);
    }

    [Fact]
    public void CanHandle_CorrectPlatform()
    {
        var (sut, _, _) = CreateHandler(BuildConfig());
        sut.CanHandle("jira", "comment_created").Should().BeTrue();
        sut.CanHandle("jira", "issue_updated").Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_ZeroMatches_HandledNoSpawn()
    {
        var (sut, resolver, spawn) = CreateHandler(BuildConfig());
        resolver.Setup(r => r.Resolve(It.IsAny<AgentSmithConfig>(), It.IsAny<IncomingTicketEnvelope>()))
            .Returns(Array.Empty<ProjectMatch>());

        var result = await sut.HandleAsync(BuildPayload(), EmptyHeaders);

        result.Handled.Should().BeTrue();
        spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(),
            It.IsAny<string>(), It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_KeywordPresent_OneMatch_DispatchesSpawn()
    {
        var (sut, resolver, spawn) = CreateHandler(BuildConfig());
        resolver.Setup(r => r.Resolve(It.IsAny<AgentSmithConfig>(), It.IsAny<IncomingTicketEnvelope>()))
            .Returns(new[] { new ProjectMatch("my-project", "fix-bug", "jira") });

        var result = await sut.HandleAsync(BuildPayload(), EmptyHeaders);

        result.Handled.Should().BeTrue();
        spawn.Verify(s => s.ExecuteAsync(
            It.IsAny<AgentSmithConfig>(),
            It.Is<ResolvedProject>(p => p.Name == "my-project"),
            "fix-bug",
            It.Is<IncomingTicketEnvelope>(e => e.TicketId == "PROJ-456" && e.Platform == "jira"),
            It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<Dictionary<string, string>?>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NoKeyword_FilteredOut_NotHandled()
    {
        var (sut, resolver, spawn) = CreateHandler(BuildConfig());
        resolver.Setup(r => r.Resolve(It.IsAny<AgentSmithConfig>(), It.IsAny<IncomingTicketEnvelope>()))
            .Returns(new[] { new ProjectMatch("my-project", "fix-bug", "jira") });

        var result = await sut.HandleAsync(
            BuildPayload(commentText: "just a regular comment"), EmptyHeaders);

        result.Handled.Should().BeFalse();
        spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(),
            It.IsAny<string>(), It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_StatusNotInTriggerStatuses_NoSpawn()
    {
        var (sut, resolver, spawn) =
            CreateHandler(BuildConfig(triggerStatuses: new List<string> { "Open" }));
        resolver.Setup(r => r.Resolve(It.IsAny<AgentSmithConfig>(), It.IsAny<IncomingTicketEnvelope>()))
            .Returns(new[] { new ProjectMatch("my-project", "fix-bug", "jira") });

        var result = await sut.HandleAsync(
            BuildPayload(issueStatus: "Closed"), EmptyHeaders);

        result.Handled.Should().BeTrue();
        spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(),
            It.IsAny<string>(), It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Never);
    }
}
