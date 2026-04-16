using System.Text.Json;
using AgentSmith.Cli.Services.Webhooks;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

public sealed class JiraCommentWebhookHandlerTests
{
    private const string ConfigPath = "test-config.yml";

    private static string BuildPayload(
        string commentText = "@agent-smith fix this",
        string issueKey = "PROJ-456",
        string issueStatus = "Open",
        string[]? labels = null)
    {
        var labelArray = labels ?? [];
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
        List<string>? triggerStatuses = null,
        string doneStatus = "In Review",
        string defaultPipeline = "fix-bug",
        Dictionary<string, string>? labelMap = null)
    {
        return new AgentSmithConfig
        {
            Projects = new Dictionary<string, ProjectConfig>
            {
                ["my-project"] = new()
                {
                    JiraTrigger = new JiraTriggerConfig
                    {
                        CommentKeyword = commentKeyword,
                        TriggerStatuses = triggerStatuses ?? ["Open"],
                        DoneStatus = doneStatus,
                        DefaultPipeline = defaultPipeline,
                        PipelineFromLabel = labelMap ?? new Dictionary<string, string>
                        {
                            ["bug"] = "fix-bug",
                            ["feature"] = "implement-feature"
                        }
                    }
                }
            }
        };
    }

    private static JiraCommentWebhookHandler CreateHandler(AgentSmithConfig config)
    {
        var configLoader = new Mock<IConfigurationLoader>();
        configLoader.Setup(c => c.LoadConfig(ConfigPath)).Returns(config);

        return new JiraCommentWebhookHandler(
            configLoader.Object,
            new ServerContext(ConfigPath),
            NullLogger<JiraCommentWebhookHandler>.Instance);
    }

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public async Task HandleAsync_KeywordPresent_StatusOpen_Triggers()
    {
        var handler = CreateHandler(BuildConfig());
        var payload = BuildPayload();

        var result = await handler.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Be("fix PROJ-456 in my-project");
        result.Pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public async Task HandleAsync_KeywordPresent_StatusClosed_DoesNotTrigger()
    {
        var handler = CreateHandler(BuildConfig());
        var payload = BuildPayload(issueStatus: "Closed");

        var result = await handler.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_NoKeyword_DoesNotTrigger()
    {
        var handler = CreateHandler(BuildConfig());
        var payload = BuildPayload(commentText: "just a regular comment");

        var result = await handler.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_NoCommentKeywordConfigured_DoesNotTrigger()
    {
        var handler = CreateHandler(BuildConfig(commentKeyword: null));
        var payload = BuildPayload(commentText: "@agent-smith fix this");

        var result = await handler.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_KeywordCaseInsensitive_Triggers()
    {
        var handler = CreateHandler(BuildConfig());
        var payload = BuildPayload(commentText: "@Agent-Smith please fix");

        var result = await handler.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_LabelMatchesPipelineMap_UsesMappedPipeline()
    {
        var handler = CreateHandler(BuildConfig());
        var payload = BuildPayload(labels: ["feature"]);

        var result = await handler.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("implement-feature");
    }

    [Fact]
    public async Task HandleAsync_SetsDoneStatusInInitialContext()
    {
        var handler = CreateHandler(BuildConfig(doneStatus: "In Review"));
        var payload = BuildPayload();

        var result = await handler.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.InitialContext.Should().ContainKey(ContextKeys.DoneStatus);
        result.InitialContext![ContextKeys.DoneStatus].Should().Be("In Review");
    }

    [Fact]
    public void CanHandle_JiraCommentCreated_ReturnsTrue()
    {
        var handler = CreateHandler(BuildConfig());
        handler.CanHandle("jira", "comment_created").Should().BeTrue();
    }

    [Fact]
    public void CanHandle_JiraIssueUpdated_ReturnsFalse()
    {
        var handler = CreateHandler(BuildConfig());
        handler.CanHandle("jira", "issue_updated").Should().BeFalse();
    }
}
