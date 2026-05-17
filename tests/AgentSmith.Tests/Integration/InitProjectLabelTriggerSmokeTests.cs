using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// p0133: confirms that the literal pipeline name "init-project" routes cleanly
/// from a label-bearing webhook payload to a WebhookResult on each of the four
/// supported platforms. Catches any regression that silently drops or rewrites
/// the init-project routing string anywhere between webhook ingress and the
/// resulting JobRequest.
/// </summary>
public sealed class InitProjectLabelTriggerSmokeTests
{
    private const string ConfigPath = "test-config.yml";
    private const string InitLabel = "agent-smith:init";
    private const string InitPipeline = "init-project";

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static Mock<IConfigurationLoader> ConfigLoader(AgentSmithConfig config)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(c => c.LoadConfig(ConfigPath)).Returns(config);
        return loader;
    }

    [Fact]
    public async Task GitHubIssueWebhookHandler_InitLabelInPayload_ReturnsWebhookResultWithInitProjectPipeline()
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["my-repo"] = new()
                {
                    Repo = new RepoConnection { Url = "https://github.com/org/my-repo" },
                    GithubTrigger = new WebhookTriggerConfig
                    {
                        PipelineFromLabel = new Dictionary<string, string>
                        {
                            [InitLabel] = InitPipeline,
                            ["bug"] = "fix-bug"
                        },
                        DoneStatus = "closed"
                    }
                }
            }
        };

        var sut = new GitHubIssueWebhookHandler(
            ConfigLoader(config).Object, new ServerContext(ConfigPath),
            NullLogger<GitHubIssueWebhookHandler>.Instance);

        var payload = $$"""
        {
            "action": "labeled",
            "label": { "name": "{{InitLabel}}" },
            "issue": { "number": 7, "state": "open" },
            "repository": { "name": "my-repo", "html_url": "https://github.com/org/my-repo" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be(InitPipeline);
        result.ProjectName.Should().Be("my-repo");
        result.TicketId.Should().Be("7");
        result.InitialContext.Should().ContainKey(ContextKeys.DoneStatus)
            .WhoseValue.Should().Be("closed");
    }

    [Fact]
    public async Task GitLabIssueWebhookHandler_InitLabelInPayload_ReturnsWebhookResultWithInitProjectPipeline()
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["my-repo"] = new()
                {
                    Repo = new RepoConnection { Url = "https://gitlab.com/org/my-repo" },
                    GitlabTrigger = new WebhookTriggerConfig
                    {
                        PipelineFromLabel = new Dictionary<string, string>
                        {
                            [InitLabel] = InitPipeline,
                            ["bug"] = "fix-bug"
                        },
                        DoneStatus = "closed"
                    }
                }
            }
        };

        var sut = new GitLabIssueWebhookHandler(
            ConfigLoader(config).Object, new ServerContext(ConfigPath),
            NullLogger<GitLabIssueWebhookHandler>.Instance);

        var payload = $$"""
        {
            "object_attributes": { "action": "open", "state": "opened", "iid": 11 },
            "project": { "web_url": "https://gitlab.com/org/my-repo" },
            "labels": [ { "title": "{{InitLabel}}" } ]
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be(InitPipeline);
        result.ProjectName.Should().Be("my-repo");
        result.TicketId.Should().Be("11");
        result.InitialContext.Should().ContainKey(ContextKeys.DoneStatus)
            .WhoseValue.Should().Be("closed");
    }

    [Fact]
    public async Task AzureDevOpsWorkItemWebhookHandler_InitTagInPayload_ReturnsWebhookResultWithInitProjectPipeline()
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["my-repo"] = new()
                {
                    Tracker = new TrackerConnection { Type = TrackerType.AzureDevOps },
                    AzuredevopsTrigger = new WebhookTriggerConfig
                    {
                        PipelineFromLabel = new Dictionary<string, string>
                        {
                            [InitLabel] = InitPipeline,
                            ["bug"] = "fix-bug"
                        },
                        DoneStatus = "Resolved"
                    }
                }
            }
        };

        var sut = new AzureDevOpsWorkItemWebhookHandler(
            ConfigLoader(config).Object, new ServerContext(ConfigPath),
            NullLogger<AzureDevOpsWorkItemWebhookHandler>.Instance);

        var payload = $$"""
        {
            "resource": {
                "id": 42,
                "fields": {
                    "System.Tags": "{{InitLabel}}; some-other-tag",
                    "System.State": "New"
                }
            }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be(InitPipeline);
        result.ProjectName.Should().Be("my-repo");
        result.TicketId.Should().Be("42");
        result.InitialContext.Should().ContainKey(ContextKeys.DoneStatus)
            .WhoseValue.Should().Be("Resolved");
    }

    [Fact]
    public async Task JiraAssigneeWebhookHandler_InitLabelInPayload_ReturnsWebhookResultWithInitProjectPipeline()
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["my-repo"] = new()
                {
                    JiraTrigger = new JiraTriggerConfig
                    {
                        AssigneeName = "Agent Smith",
                        TriggerStatuses = ["Open"],
                        PipelineFromLabel = new Dictionary<string, string>
                        {
                            [InitLabel] = InitPipeline,
                            ["bug"] = "fix-bug"
                        },
                        DoneStatus = "Done"
                    }
                }
            }
        };

        var sut = new JiraAssigneeWebhookHandler(
            ConfigLoader(config).Object, new ServerContext(ConfigPath),
            NullLogger<JiraAssigneeWebhookHandler>.Instance);

        var labelsJson = JsonSerializer.Serialize(new[] { InitLabel });
        var payload = $$"""
        {
            "webhookEvent": "jira:issue_updated",
            "issue": {
                "key": "PROJ-99",
                "fields": {
                    "assignee": { "displayName": "Agent Smith" },
                    "status": { "name": "Open" },
                    "labels": {{labelsJson}}
                }
            },
            "changelog": {
                "items": [
                    {
                        "field": "assignee",
                        "fieldtype": "jira",
                        "fromString": "Alice",
                        "toString": "Agent Smith"
                    }
                ]
            }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.Pipeline.Should().Be(InitPipeline);
        result.ProjectName.Should().Be("my-repo");
        result.TicketId.Should().Be("PROJ-99");
        result.InitialContext.Should().ContainKey(ContextKeys.DoneStatus)
            .WhoseValue.Should().Be("Done");
    }
}
