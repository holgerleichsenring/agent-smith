using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Spawning;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.EndToEnd;

/// <summary>
/// p0140b regression: single-repo projects should still produce a PipelineRequest with the
/// same shape as before, plus the new <see cref="PipelineRequest.RepoName"/> field. One
/// test per tracker family wires the real ProjectResolver + SpawnPipelineRunsUseCase +
/// TicketClaimService stack and captures the enqueued PipelineRequest via a mock IRedisJobQueue.
/// </summary>
public sealed class SingleRepoRegressionTests
{
    private const string ConfigPath = "test-config.yml";

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public async Task SingleRepo_GitHub_EnqueuesExpectedPipelineRequest()
    {
        var config = SingleRepoConfig(
            project => project with { GithubTrigger = NewTrigger() },
            repoUrl: "https://github.com/org/my-repo");
        var (sut, captured) = BuildGitHubHandler(config);

        var payload = """
        {
            "action": "labeled",
            "label": { "name": "agent-smith" },
            "issue": {
                "number": 7, "state": "open",
                "labels": [{ "name": "agent-smith" }]
            },
            "repository": { "name": "my-repo", "html_url": "https://github.com/org/my-repo" }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        AssertCanonicalShape(captured.Value, "github", "7", "fix-bug", "my-repo", "closed");
    }

    [Fact]
    public async Task SingleRepo_GitLab_EnqueuesExpectedPipelineRequest()
    {
        var config = SingleRepoConfig(
            project => project with { GitlabTrigger = NewTrigger() },
            repoUrl: "https://gitlab.com/org/my-repo");
        var (sut, captured) = BuildGitLabHandler(config);

        var payload = """
        {
            "object_attributes": { "action": "open", "state": "opened", "iid": 11 },
            "project": { "web_url": "https://gitlab.com/org/my-repo" },
            "labels": [ { "title": "agent-smith" } ]
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        AssertCanonicalShape(captured.Value, "gitlab", "11", "fix-bug", "my-repo", "closed");
    }

    [Fact]
    public async Task SingleRepo_AzureDevOps_EnqueuesExpectedPipelineRequest()
    {
        var config = SingleRepoConfig(
            project => project with { AzuredevopsTrigger = NewTrigger() });
        var (sut, captured) = BuildAdoHandler(config);

        var payload = """
        {
            "resource": {
                "id": 42,
                "fields": { "System.Tags": "agent-smith", "System.State": "New" }
            }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        AssertCanonicalShape(captured.Value, "azuredevops", "42", "fix-bug", "my-repo", "closed");
    }

    [Fact]
    public async Task SingleRepo_Jira_EnqueuesExpectedPipelineRequest()
    {
        var config = SingleRepoConfig(project => project with
        {
            JiraTrigger = new JiraTriggerConfig
            {
                AssigneeName = "Agent Smith",
                TriggerStatuses = new List<string> { "Open" },
                ProjectResolution = new ProjectResolutionConfig
                {
                    Strategy = ResolutionStrategy.Tag, Value = "agent-smith"
                },
                DefaultPipeline = "fix-bug",
                DoneStatus = "closed"
            }
        });
        var (sut, captured) = BuildJiraAssigneeHandler(config);

        var payload = """
        {
            "webhookEvent": "jira:issue_updated",
            "issue": {
                "key": "PROJ-99",
                "fields": {
                    "assignee": { "displayName": "Agent Smith" },
                    "status": { "name": "Open" },
                    "labels": ["agent-smith"]
                }
            },
            "changelog": {
                "items": [
                    { "field": "assignee", "fromString": "Alice", "toString": "Agent Smith" }
                ]
            }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        AssertCanonicalShape(captured.Value, "jira", "PROJ-99", "fix-bug", "my-repo", "closed");
    }

    private static void AssertCanonicalShape(
        PipelineRequest? captured, string platform, string ticketId,
        string pipelineName, string repoName, string doneStatus)
    {
        captured.Should().NotBeNull("expected exactly one EnqueueAsync call");
        captured!.ProjectName.Should().Be("my-repo");
        captured.PipelineName.Should().Be(pipelineName);
        captured.TicketId!.Value.Should().Be(ticketId);
        captured.Headless.Should().BeTrue();
        captured.PlanAnswers.Should().BeNull();
        captured.RepoName.Should().Be(repoName);
        captured.Context.Should().NotBeNull();
        captured.Context![ContextKeys.DoneStatus].Should().Be(doneStatus);
    }

    private static WebhookTriggerConfig NewTrigger() => new()
    {
        ProjectResolution = new ProjectResolutionConfig
        {
            Strategy = ResolutionStrategy.Tag, Value = "agent-smith"
        },
        DefaultPipeline = "fix-bug",
        DoneStatus = "closed"
    };

    private static AgentSmithConfig SingleRepoConfig(
        Func<ResolvedProject, ResolvedProject> configureTriggers,
        string repoUrl = "")
    {
        var project = configureTriggers(new ResolvedProject
        {
            Name = "my-repo",
            Repo = new RepoConnection { Name = "my-repo", Url = repoUrl }
        });
        return new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject> { ["my-repo"] = project }
        };
    }

    // --- Handler builders share a single Stack mock + capture container -----------------

    private sealed class CapturedRequest
    {
        public PipelineRequest? Value { get; set; }
    }

    private static (TStack stack, CapturedRequest captured) BuildStack<TStack>(
        AgentSmithConfig config, Func<IConfigurationLoader, ServerContext, IEnvelopeProjectResolver,
            WebhookSpawnDispatcher, TStack> ctor)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(c => c.LoadConfig(ConfigPath)).Returns(config);

        var captured = new CapturedRequest();
        var queue = new Mock<IRedisJobQueue>();
        queue.Setup(q => q.EnqueueAsync(It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PipelineRequest, CancellationToken>((r, _) => captured.Value = r)
            .Returns(Task.CompletedTask);

        var claimLock = new Mock<IRedisClaimLock>();
        claimLock.Setup(l => l.TryAcquireAsync(
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");
        claimLock.Setup(l => l.ReleaseAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var transitioner = new Mock<ITicketStatusTransitioner>();
        transitioner.Setup(t => t.ReadCurrentAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TicketLifecycleStatus?)null);
        transitioner.Setup(t => t.TransitionAsync(
                It.IsAny<TicketId>(), It.IsAny<TicketLifecycleStatus>(),
                It.IsAny<TicketLifecycleStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransitionResult.Succeeded());

        var factory = new Mock<ITicketStatusTransitionerFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(transitioner.Object);

        var claimService = new TicketClaimService(
            claimLock.Object, factory.Object, queue.Object,
            NullLogger<TicketClaimService>.Instance);
        var spawn = new SpawnPipelineRunsUseCase(
            claimService, NullLogger<SpawnPipelineRunsUseCase>.Instance);

        var providerFactory = new Mock<ITicketProviderFactory>();
        var dispatcher = new WebhookSpawnDispatcher(
            spawn, providerFactory.Object, NullLogger<WebhookSpawnDispatcher>.Instance);

        var resolver = new ProjectResolver(NullLogger<ProjectResolver>.Instance);
        var sut = ctor(loader.Object, new ServerContext(ConfigPath), resolver, dispatcher);
        return (sut, captured);
    }

    private static (GitHubIssueWebhookHandler sut, CapturedRequest captured)
        BuildGitHubHandler(AgentSmithConfig config) =>
        BuildStack(config, (l, c, r, d) =>
            new GitHubIssueWebhookHandler(l, c, r, d,
                NullLogger<GitHubIssueWebhookHandler>.Instance));

    private static (GitLabIssueWebhookHandler sut, CapturedRequest captured)
        BuildGitLabHandler(AgentSmithConfig config) =>
        BuildStack(config, (l, c, r, d) =>
            new GitLabIssueWebhookHandler(l, c, r, d,
                NullLogger<GitLabIssueWebhookHandler>.Instance));

    private static (AzureDevOpsWorkItemWebhookHandler sut, CapturedRequest captured)
        BuildAdoHandler(AgentSmithConfig config) =>
        BuildStack(config, (l, c, r, d) =>
            new AzureDevOpsWorkItemWebhookHandler(l, c, r, d,
                NullLogger<AzureDevOpsWorkItemWebhookHandler>.Instance));

    private static (JiraAssigneeWebhookHandler sut, CapturedRequest captured)
        BuildJiraAssigneeHandler(AgentSmithConfig config) =>
        BuildStack(config, (l, c, r, d) =>
            new JiraAssigneeWebhookHandler(l, c, r, d,
                NullLogger<JiraAssigneeWebhookHandler>.Instance));
}
