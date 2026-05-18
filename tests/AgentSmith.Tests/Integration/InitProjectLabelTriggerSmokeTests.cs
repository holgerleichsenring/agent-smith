using System.Text.Json;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// p0140b: confirms the literal pipeline name "init-project" routes cleanly from a
/// label-bearing webhook payload through real ProjectResolver and real
/// WebhookSpawnDispatcher down to a verified ISpawnPipelineRunsUseCase.ExecuteAsync
/// invocation for each of the four supported platforms.
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

    private static (WebhookSpawnDispatcher dispatcher, Mock<ISpawnPipelineRunsUseCase> spawn)
        BuildDispatcher()
    {
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
        return (dispatcher, spawn);
    }

    private static ProjectResolver Resolver() => new(NullLogger<ProjectResolver>.Instance);

    [Fact]
    public async Task GitHubIssueWebhookHandler_InitLabelInPayload_SpawnsInitProject()
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["my-repo"] = new()
                {
                    Name = "my-repo",
                    Repo = new RepoConnection { Name = "my-repo", Url = "https://github.com/org/my-repo" },
                    GithubTrigger = new WebhookTriggerConfig
                    {
                        ProjectResolution = new ProjectResolutionConfig
                        {
                            Strategy = ResolutionStrategy.Repo, Value = ""
                        },
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
        var (dispatcher, spawn) = BuildDispatcher();
        var sut = new GitHubIssueWebhookHandler(
            ConfigLoader(config).Object, new ServerContext(ConfigPath),
            Resolver(), dispatcher,
            NullLogger<GitHubIssueWebhookHandler>.Instance);

        var payload = $$"""
        {
            "action": "labeled",
            "label": { "name": "{{InitLabel}}" },
            "issue": {
                "number": 7,
                "state": "open",
                "labels": [{ "name": "{{InitLabel}}" }]
            },
            "repository": { "name": "my-repo", "html_url": "https://github.com/org/my-repo" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        spawn.Verify(s => s.ExecuteAsync(
            It.IsAny<AgentSmithConfig>(),
            It.Is<ResolvedProject>(p => p.Name == "my-repo"),
            InitPipeline,
            It.Is<IncomingTicketEnvelope>(e => e.TicketId == "7" && e.Platform == "github"),
            It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<Dictionary<string, string>?>()), Times.Once);
    }

    [Fact]
    public async Task GitLabIssueWebhookHandler_InitLabelInPayload_SpawnsInitProject()
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["my-repo"] = new()
                {
                    Name = "my-repo",
                    Repo = new RepoConnection { Name = "my-repo", Url = "https://gitlab.com/org/my-repo" },
                    GitlabTrigger = new WebhookTriggerConfig
                    {
                        ProjectResolution = new ProjectResolutionConfig
                        {
                            Strategy = ResolutionStrategy.Tag, Value = InitLabel
                        },
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
        var (dispatcher, spawn) = BuildDispatcher();
        var sut = new GitLabIssueWebhookHandler(
            ConfigLoader(config).Object, new ServerContext(ConfigPath),
            Resolver(), dispatcher,
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
        spawn.Verify(s => s.ExecuteAsync(
            It.IsAny<AgentSmithConfig>(),
            It.Is<ResolvedProject>(p => p.Name == "my-repo"),
            InitPipeline,
            It.Is<IncomingTicketEnvelope>(e => e.TicketId == "11" && e.Platform == "gitlab"),
            It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<Dictionary<string, string>?>()), Times.Once);
    }

    [Fact]
    public async Task AzureDevOpsWorkItemWebhookHandler_InitTagInPayload_SpawnsInitProject()
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["my-repo"] = new()
                {
                    Name = "my-repo",
                    Tracker = new TrackerConnection { Type = TrackerType.AzureDevOps },
                    Repo = new RepoConnection { Name = "my-repo" },
                    AzuredevopsTrigger = new WebhookTriggerConfig
                    {
                        ProjectResolution = new ProjectResolutionConfig
                        {
                            Strategy = ResolutionStrategy.Tag, Value = InitLabel
                        },
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
        var (dispatcher, spawn) = BuildDispatcher();
        var sut = new AzureDevOpsWorkItemWebhookHandler(
            ConfigLoader(config).Object, new ServerContext(ConfigPath),
            Resolver(), dispatcher,
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
        spawn.Verify(s => s.ExecuteAsync(
            It.IsAny<AgentSmithConfig>(),
            It.Is<ResolvedProject>(p => p.Name == "my-repo"),
            InitPipeline,
            It.Is<IncomingTicketEnvelope>(e => e.TicketId == "42" && e.Platform == "azuredevops"),
            It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<Dictionary<string, string>?>()), Times.Once);
    }

    [Fact]
    public async Task JiraAssigneeWebhookHandler_InitLabelInPayload_SpawnsInitProject()
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["my-repo"] = new()
                {
                    Name = "my-repo",
                    Repo = new RepoConnection { Name = "my-repo" },
                    JiraTrigger = new JiraTriggerConfig
                    {
                        AssigneeName = "Agent Smith",
                        TriggerStatuses = new List<string> { "Open" },
                        ProjectResolution = new ProjectResolutionConfig
                        {
                            Strategy = ResolutionStrategy.Tag, Value = InitLabel
                        },
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
        var (dispatcher, spawn) = BuildDispatcher();
        var sut = new JiraAssigneeWebhookHandler(
            ConfigLoader(config).Object, new ServerContext(ConfigPath),
            Resolver(), dispatcher,
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
        spawn.Verify(s => s.ExecuteAsync(
            It.IsAny<AgentSmithConfig>(),
            It.Is<ResolvedProject>(p => p.Name == "my-repo"),
            InitPipeline,
            It.Is<IncomingTicketEnvelope>(e => e.TicketId == "PROJ-99" && e.Platform == "jira"),
            It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<Dictionary<string, string>?>()), Times.Once);
    }
}
