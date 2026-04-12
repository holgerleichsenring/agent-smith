using System.Text.Json;
using AgentSmith.Cli.Services.Webhooks;
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
        string changeField = "assignee")
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
                        AssigneeName = assigneeName,
                        DefaultPipeline = defaultPipeline,
                        PipelineFromLabel = labelMap ?? new Dictionary<string, string>
                        {
                            ["security-review"] = "security-scan",
                            ["mad-discussion"] = "mad-discussion"
                        }
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
        result.TriggerInput.Should().Be("fix PROJ-123 in my-project");
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
        // Config order: security-review first, mad-discussion second.
        // Payload labels: mad-discussion appears before security-review.
        // Config order should win → security-scan.
        var handler = CreateHandler(BuildConfig());
        var payload = BuildPayload(labels: ["mad-discussion", "security-review"]);

        var result = await handler.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be("security-scan");
    }

    [Fact]
    public async Task HandleAsync_SecretMissing_SkipsVerification()
    {
        // No secret configured → handler should still process the payload
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
        // This tests the WebhookSignatureValidator.ValidateJira method directly,
        // since signature validation is done by the WebhookListener before dispatch.
        var result = WebhookSignatureValidator.ValidateJira(
            "some payload", signatureHeader: null, secret: "my-secret");

        result.Should().BeFalse();
    }
}
