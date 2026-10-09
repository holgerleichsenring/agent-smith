using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Sweep;
using AgentSmith.Server.Services.Webhooks;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

/// <summary>2026-10-08-101b: a polling tracker entry's projects get nothing from webhooks, and the list says why.</summary>
public sealed class TriggerModeTests
{
    private static readonly RepoConnection Repo = new() { Name = "r", Url = "https://github.com/o/r", Type = RepoType.GitHub };

    private static ResolvedProject Project(string name, bool polls) => new()
    {
        Name = name, Repos = [Repo],
        Tracker = new TrackerConnection { Name = $"{name}-tracker", Type = TrackerType.GitHub, Polling = new PollingConfig { Enabled = polls } },
        GithubTrigger = new WebhookTriggerConfig { TriggerStatuses = ["open"], DefaultPipeline = "code" },
    };

    private static AgentSmithConfig Config(params ResolvedProject[] projects) => new()
    {
        Projects = projects.ToDictionary(p => p.Name),
        Trackers = projects.ToDictionary(p => p.Tracker.Name, p => p.Tracker),
    };

    private static TriggerModeGate Gate(AgentSmithConfig config)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(config);
        return new TriggerModeGate(loader.Object, new ServerContext("c.yml"), new ConfiguredRepoFinder());
    }

    private const string Labeled = """
        { "action": "labeled", "issue": { "number": 7, "state": "open", "labels": [ { "name": "go" } ], "html_url": "https://github.com/o/r/issues/7" },
          "label": { "name": "go" }, "repository": { "html_url": "https://github.com/o/r" } }
        """;

    private static (GitHubIssueWebhookHandler Handler, Mock<ISpawnPipelineRunsUseCase> Spawn, Mock<ITicketProvider> Ticket) Issues(AgentSmithConfig config)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(config);
        var resolver = new Mock<IEnvelopeProjectResolver>();
        resolver.Setup(r => r.Resolve(It.IsAny<AgentSmithConfig>(), It.IsAny<IncomingTicketEnvelope>())).Returns([new ProjectMatch("p", "code", "github")]);
        var spawn = new Mock<ISpawnPipelineRunsUseCase>();
        var ticket = new Mock<ITicketProvider>();
        var tickets = new Mock<ITicketProviderFactory>();
        tickets.Setup(t => t.Create(It.IsAny<TrackerConnection>())).Returns(ticket.Object);
        var dispatcher = new WebhookSpawnDispatcher(spawn.Object, tickets.Object, NullLogger<WebhookSpawnDispatcher>.Instance);
        return (new GitHubIssueWebhookHandler(loader.Object, new ServerContext("c.yml"), resolver.Object, dispatcher,
            ApprovedRecordProbes.None(), NullLogger<GitHubIssueWebhookHandler>.Instance, modeGate: Gate(config)), spawn, ticket);
    }

    [Fact]
    public async Task WebhookGate_PollingProject_NotHandledWithReason()
    {
        var (handler, spawn, _) = Issues(Config(Project("p", polls: true)));

        var result = await handler.HandleAsync(Labeled, new Dictionary<string, string>());

        result.Handled.Should().BeFalse();
        result.SkipReason.Should().Be("project p polls tracker p-tracker; webhooks start nothing for it");
        spawn.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WebhookGate_AllGated_NoZeroMatchComment()
    {
        var (handler, _, ticket) = Issues(Config(Project("p", polls: true)));

        await handler.HandleAsync(Labeled, new Dictionary<string, string>());

        ticket.Verify(t => t.UpdateStatusAsync(It.IsAny<AgentSmith.Domain.Models.TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WebhookGate_PrCommand_NoModelCall()
    {
        var model = new Mock<IIntentParser>(MockBehavior.Strict);
        var admission = new PrCommentCommandAdmission(new CommentIntentParser(model.Object), new ServerContext("c.yml"),
            Mock.Of<IPrCommandLaunch>(), NullLogger<PrCommentCommandAdmission>.Instance);
        var handler = new GitHubPrCommentWebhookHandler(admission, Mock.Of<IPrCommentAuthorTrust>(), NullLogger<GitHubPrCommentWebhookHandler>.Instance,
            modeGate: Gate(Config(Project("p", polls: true))));
        const string comment = """
            { "action": "created", "issue": { "number": 4, "pull_request": { "url": "x" } },
              "comment": { "body": "/agent-smith review this", "user": { "login": "alice" }, "author_association": "MEMBER" },
              "repository": { "full_name": "o/r", "html_url": "https://github.com/o/r" } }
            """;

        (await handler.HandleAsync(comment, new Dictionary<string, string>())).SkipReason.Should().Contain("polls tracker");
    }

    [Fact]
    public void WebhookGate_MixedModeRepo_Gated() =>
        Gate(Config(Project("p", polls: false), Project("q", polls: true))).RepoRefusal("https://github.com/o/r.git")
            .Should().NotBeNull("any polling owner of the repository gates its deliveries");

    [Fact]
    public void Probe_MixedModeRepository_Advisory()
    {
        var findings = TriggerModeFindings.Findings(Config(Project("p", polls: false), Project("q", polls: true))).ToList();

        findings.Should().ContainSingle(f => f.Severity == StartupFindingSeverity.Advisory && f.Reason.Contains("github.com/o/r"));
    }

    [Fact]
    public async Task Dispatch_GitHubIssueCommentPair_ShowsReason()
    {
        IWebhookHandler[] handlers = [Refusing("issue_comment", "first"), Refusing("issue_comment", null)];

        (await WebhookRequestProcessor.DispatchAsync(handlers, "github", "issue_comment", "{}", new Dictionary<string, string>()))
            .SkipReason.Should().Be("first", "the last reason given is the one shown");
    }

    [Fact]
    public async Task Dispatch_GitHubPullRequestPair_ShowsReason()
    {
        IWebhookHandler[] handlers = [Refusing("pull_request", "label not asked"), Refusing("pull_request", "project p polls tracker t")];

        (await WebhookRequestProcessor.DispatchAsync(handlers, "github", "pull_request", "{}", new Dictionary<string, string>()))
            .SkipReason.Should().Be("project p polls tracker t");
    }

    [Fact]
    public async Task PrSweep_PollingProject_StillServed()
    {
        var launcher = new Mock<IDetachedPipelineLauncher>();
        var admission = new PrCommentCommandAdmission(new CommentIntentParser(Mock.Of<IIntentParser>()), new ServerContext("c.yml"),
            Mock.Of<IPrCommandLaunch>(), NullLogger<PrCommentCommandAdmission>.Instance);
        var actions = new PrSweepActions(new PrReviewRouteResolver(new ConfiguredRepoFinder()), new PrTriggerLabelResolver(),
            new PrRunContextFactory(), launcher.Object, admission,
            new AgentSmith.Server.Services.Sweep.PrSweepBreaker(Mock.Of<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(),
                Mock.Of<AgentSmith.Contracts.Providers.ISourceProviderFactory>(), NullLogger<AgentSmith.Server.Services.Sweep.PrSweepBreaker>.Instance),
            Mock.Of<IServiceProvider>());
        var pr = new OpenPullRequest("4", "https://github.com/o/r/pull/4", "h", "b", "f", "alice", [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        await actions.ReviewAsync(new SweepTarget(Config(Project("p", polls: true)), Repo, new HashSet<string> { "p" }), "github.com/o/r", pr);

        launcher.Verify(l => l.LaunchAsync("p", "pr-review", It.IsAny<Dictionary<string, object>?>(), It.IsAny<Func<bool, Task>>()),
            Times.Once, "the resolvers are not gated");
    }

    private static IWebhookHandler Refusing(string eventType, string? reason)
    {
        var handler = new Mock<IWebhookHandler>();
        handler.Setup(h => h.CanHandle("github", eventType)).Returns(true);
        handler.Setup(h => h.HandleAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reason is null ? WebhookResult.NotHandled() : WebhookResult.NotHandled(reason));
        return handler.Object;
    }
}
