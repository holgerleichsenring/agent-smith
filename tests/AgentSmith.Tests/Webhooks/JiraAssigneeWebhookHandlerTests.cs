using System.Text.Json;
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

public sealed class JiraAssigneeWebhookHandlerTests
{
    private const string ConfigPath = "test-config.yml";

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static string BuildPayload(
        string assigneeName = "Agent Smith",
        string issueKey = "PROJ-123",
        string[]? labels = null,
        string changeField = "assignee",
        string issueStatus = "Open")
    {
        var labelArray = labels ?? Array.Empty<string>();
        var labelsJson = JsonSerializer.Serialize(labelArray);
        return $$"""
        {
          "webhookEvent": "jira:issue_updated",
          "issue": {
            "key": "{{issueKey}}",
            "fields": {
              "assignee": { "displayName": "{{assigneeName}}" },
              "status": { "name": "{{issueStatus}}" },
              "labels": {{labelsJson}}
            }
          },
          "changelog": {
            "items": [
              {
                "field": "{{changeField}}",
                "fieldtype": "jira",
                "fromString": "Alice Example",
                "toString": "{{assigneeName}}"
              }
            ]
          }
        }
        """;
    }

    private static AgentSmithConfig BuildConfig(
        string assigneeName = "Agent Smith",
        List<string>? triggerStatuses = null)
    {
        return new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["my-project"] = new()
                {
                    Name = "my-project",
                    Repos = new[] { new RepoConnection { Name = "my-project" } },
                    JiraTrigger = new JiraTriggerConfig
                    {
                        AssigneeName = assigneeName,
                        ProjectResolution = new ProjectResolutionConfig
                        {
                            Strategy = ResolutionStrategy.Tag, Value = "agent-smith"
                        },
                        DefaultPipeline = "fix-bug",
                        TriggerStatuses = triggerStatuses ?? new List<string> { "Open" },
                        DoneStatus = "In Review"
                    }
                }
            }
        };
    }

    private static (JiraAssigneeWebhookHandler handler,
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
        var handler = new JiraAssigneeWebhookHandler(
            loader.Object, new ServerContext(ConfigPath),
            resolver.Object, dispatcher,
            NullLogger<JiraAssigneeWebhookHandler>.Instance);
        return (handler, resolver, spawn);
    }

    [Fact]
    public void CanHandle_CorrectPlatform()
    {
        var (sut, _, _) = CreateHandler(BuildConfig());
        sut.CanHandle("jira", "issue_updated").Should().BeTrue();
        sut.CanHandle("jira", "comment_created").Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_NotAnAssigneeChange_ReturnsNotHandled()
    {
        var (sut, _, spawn) = CreateHandler(BuildConfig());
        var payload = BuildPayload(changeField: "status");

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
        spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(),
            It.IsAny<string>(), It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Never);
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
    public async Task HandleAsync_AssigneeMatches_OneMatch_DispatchesSpawn()
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
            It.Is<IncomingTicketEnvelope>(e => e.TicketId == "PROJ-123" && e.Platform == "jira"),
            It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<Dictionary<string, string>?>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AssigneeMismatch_FilteredOut_NoSpawn()
    {
        var (sut, resolver, spawn) = CreateHandler(BuildConfig());
        // resolver returns the match, but handler-level filter rejects because
        // assignee != AssigneeName.
        resolver.Setup(r => r.Resolve(It.IsAny<AgentSmithConfig>(), It.IsAny<IncomingTicketEnvelope>()))
            .Returns(new[] { new ProjectMatch("my-project", "fix-bug", "jira") });

        var result = await sut.HandleAsync(BuildPayload(assigneeName: "Someone Else"), EmptyHeaders);

        result.Handled.Should().BeFalse();
        spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(),
            It.IsAny<string>(), It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_StatusNotInTriggerStatuses_NoSpawn()
    {
        var (sut, resolver, spawn) =
            CreateHandler(BuildConfig(triggerStatuses: new List<string> { "Open", "Active" }));
        resolver.Setup(r => r.Resolve(It.IsAny<AgentSmithConfig>(), It.IsAny<IncomingTicketEnvelope>()))
            .Returns(new[] { new ProjectMatch("my-project", "fix-bug", "jira") });

        var result = await sut.HandleAsync(BuildPayload(issueStatus: "In Review"), EmptyHeaders);

        result.Handled.Should().BeTrue();
        spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(),
            It.IsAny<string>(), It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Never);
    }
}
