using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Webhooks;
using AgentSmith.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks.Routing;

/// <summary>Builds the ticket-event handlers over one spawn mock and one resolver that always
/// matches 'p' — so a test reads only whether a delivery reached a spawn.</summary>
internal sealed class TicketCommentHandlerFixture
{
    private const string ConfigPath = "routing.yml";
    public Mock<ISpawnPipelineRunsUseCase> Spawn { get; } = new();
    private readonly Mock<IConfigurationLoader> _loader = new();
    private readonly Mock<IEnvelopeProjectResolver> _resolver = new();
    public static readonly IDictionary<string, string> NoHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public TicketCommentHandlerFixture(TrackerType tracker)
    {
        var trigger = new WebhookTriggerConfig
        {
            CommentKeyword = "@agent-smith", TriggerStatuses = ["New", "open", "opened"],
            ProjectResolution = new ProjectResolutionConfig { Strategy = ResolutionStrategy.Tag, Value = "x" },
        };
        var project = new ResolvedProject
        {
            Name = "p", Tracker = new TrackerConnection { Type = tracker },
            Repos = [new RepoConnection { Name = "p" }],
            AzuredevopsTrigger = trigger, GithubTrigger = trigger, GitlabTrigger = trigger,
        };
        _loader.Setup(l => l.LoadConfig(ConfigPath)).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject> { ["p"] = project },
        });
        _resolver.Setup(r => r.Resolve(It.IsAny<AgentSmithConfig>(), It.IsAny<IncomingTicketEnvelope>()))
            .Returns([new ProjectMatch("p", "code", tracker.ToString().ToLowerInvariant())]);
        Spawn.Setup(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(),
                It.IsAny<string>(), It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(),
                It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()))
            .ReturnsAsync(new SpawnResult(Array.Empty<ClaimResult>()));
    }

    private WebhookSpawnDispatcher Dispatcher() => new(
        Spawn.Object, new Mock<ITicketProviderFactory>().Object, NullLogger<WebhookSpawnDispatcher>.Instance);

    private static PlanAnswerParser Answers() => new(NullLogger<PlanAnswerParser>.Instance);

    public AzureDevOpsWorkItemCommentWebhookHandler AzureDevOpsComment() => new(
        _loader.Object, new ServerContext(ConfigPath), _resolver.Object, Dispatcher(),
        ApprovedRecordProbes.None(), Answers(), NullLogger<AzureDevOpsWorkItemCommentWebhookHandler>.Instance);

    public AzureDevOpsWorkItemWebhookHandler AzureDevOpsUpdated() => new(
        _loader.Object, new ServerContext(ConfigPath), _resolver.Object, Dispatcher(),
        ApprovedRecordProbes.None(), NullLogger<AzureDevOpsWorkItemWebhookHandler>.Instance);

    public GitHubIssueCommentWebhookHandler GitHubComment() => new(
        _loader.Object, new ServerContext(ConfigPath), _resolver.Object, Dispatcher(),
        ApprovedRecordProbes.None(), Answers(), NullLogger<GitHubIssueCommentWebhookHandler>.Instance);

    public GitLabIssueCommentWebhookHandler GitLabComment() => new(
        _loader.Object, new ServerContext(ConfigPath), _resolver.Object, Dispatcher(),
        ApprovedRecordProbes.None(), Answers(), NullLogger<GitLabIssueCommentWebhookHandler>.Instance);

    public GitLabIssueWebhookHandler GitLabIssue() => new(
        _loader.Object, new ServerContext(ConfigPath), _resolver.Object, Dispatcher(),
        ApprovedRecordProbes.None(), NullLogger<GitLabIssueWebhookHandler>.Instance);

    public void VerifySpawned(string ticketId, Times times) => Spawn.Verify(s => s.ExecuteAsync(
        It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(), It.IsAny<string>(),
        It.Is<IncomingTicketEnvelope>(e => e.TicketId == ticketId), It.IsAny<WebhookTriggerConfig>(),
        It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), times);
}
