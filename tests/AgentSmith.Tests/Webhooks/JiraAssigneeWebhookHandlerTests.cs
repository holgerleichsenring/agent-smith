using System.Text.Json;
using AgentSmith.Server.Services.Webhooks;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

public sealed class JiraAssigneeWebhookHandlerTests
{
    private const string ConfigPath = "test-config.yml";

    private static string BuildPayload(
        string assigneeName = "Agent Smith",
        string issueKey = "PROJ-123",
        string[]? labels = null,
        string changeField = "assignee",
        string issueStatus = "Open")
    {
        var labelArray = labels ?? [];
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
        string defaultPipeline = "fix-bug",
        Dictionary<string, string>? labelMap = null,
        List<string>? triggerStatuses = null,
        string doneStatus = "In Review")
    {
        return new AgentSmithConfig
        {
            Projects = new Dictionary<string, ProjectConfig>
            {
                ["my-project"] = new()
                {
                    JiraTrigger = new JiraTriggerConfig
                    {
                        AssigneeName = assigneeName,
                        DefaultPipeline = defaultPipeline,
                        PipelineFromLabel = labelMap ?? new Dictionary<string, string>
                        {
                            ["security-review"] = "security-scan",
                            ["mad-discussion"] = "mad-discussion"
                        },
                        TriggerStatuses = triggerStatuses ?? ["Open"],
                        DoneStatus = doneStatus
                    }
                }
            }
        };
    }

    private static JiraAssigneeWebhookHandler CreateHandler(AgentSmithConfig config)
    {
        var configLoader = new Mock<IConfigurationLoader>();
        configLoader.Setup(c => c.LoadConfig(ConfigPath)).Returns(config);

        return new JiraAssigneeWebhookHandler(
            configLoader.Object,
            new ServerContext(ConfigPath),
            NullLogger<JiraAssigneeWebhookHandler>.Instance);
    }

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public async Task HandleAsync_AssigneeMatchesConfig_EnqueuesJob()
    {
        var handler = CreateHandler(BuildConfig());
        var payload = BuildPayload();

        var result = await handler.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.ProjectName.Should().Be("my-project");
        result.TicketId.Should().Be("PROJ-123");
        result.Pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public async Task HandleAsync_AssigneeDoesNotMatch_ReturnsIgnored()
    {
        var handler = CreateHandler(BuildConfig());
        var payload = BuildPayload(assigneeName: "Alice Example");

        var result = await handler.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_NotAnAssigneeChange_ReturnsIgnored()
    {
        var handler = CreateHandler(BuildConfig());
        var payload = BuildPayload(changeField: "status");

        var result = await handler.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_LabelMatchesPipelineMap_UsesMappedPipeline()
    {
        var handler = CreateHandler(BuildConfig());
        var payload = BuildPayload(labels: ["security-review"]);

        var result = await handler.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("security-scan");
    }

    [Fact]
    public async Task HandleAsync_NoLabelMatch_UsesDefaultPipeline()
    {
        var handler = CreateHandler(BuildConfig());
        var payload = BuildPayload(labels: ["backend"]);

        var result = await handler.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public async Task HandleAsync_NoLabels_UsesDefaultPipeline()
    {
        var handler = CreateHandler(BuildConfig());
        var payload = BuildPayload(labels: []);

        var result = await handler.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public async Task HandleAsync_MultipleLabels_ConfigOrderWins()
    {
        var handler = CreateHandler(BuildConfig());
        var payload = BuildPayload(labels: ["mad-discussion", "security-review"]);

        var result = await handler.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("security-scan");
    }

    [Fact]
    public async Task HandleAsync_SecretMissing_SkipsVerification()
    {
        var config = BuildConfig();
        config.Projects["my-project"].JiraTrigger!.Secret = null;
        var handler = CreateHandler(config);
        var payload = BuildPayload();

        var result = await handler.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
    }

    [Fact]
    public void HandleAsync_SecretConfigured_HeaderMissing_ReturnsUnauthorized()
    {
        var result = WebhookSignatureValidator.ValidateJira(
            "some payload", signatureHeader: null, secret: "my-secret");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_StatusNotInWhitelist_DoesNotTrigger()
    {
        var handler = CreateHandler(BuildConfig(triggerStatuses: ["Open", "Active"]));
        var payload = BuildPayload(issueStatus: "In Review");

        var result = await handler.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_StatusInWhitelist_Triggers()
    {
        var handler = CreateHandler(BuildConfig(triggerStatuses: ["Open", "Active"]));
        var payload = BuildPayload(issueStatus: "Active");

        var result = await handler.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.ProjectName.Should().Be("my-project");
        result.TicketId.Should().Be("PROJ-123");
    }

    [Fact]
    public async Task HandleAsync_StatusCheckIsCaseInsensitive()
    {
        var handler = CreateHandler(BuildConfig(triggerStatuses: ["open"]));
        var payload = BuildPayload(issueStatus: "Open");

        var result = await handler.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
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
}
