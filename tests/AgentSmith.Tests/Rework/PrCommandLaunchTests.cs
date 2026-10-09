using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Services.ChatLaunch;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-08-e8b9e: a ticketed PR command goes through the funnel's claim and is answered
/// on the pull request; a live run is named, never taken over.</summary>
public sealed class PrCommandLaunchTests
{
    private readonly Mock<ISpawnPipelineRunsUseCase> _spawn = new();
    private readonly Mock<IActiveRunLease> _leases = new();
    private readonly Mock<IPrCommentProvider> _comments = new();

    private static AgentSmithConfig Config() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["api"] = new()
            {
                Name = "api", Tracker = new TrackerConnection { Type = TrackerType.GitHub },
                Repos = [new RepoConnection { Name = "api", Url = "https://github.com/o/api", Type = RepoType.GitHub }],
            },
        },
    };

    private PrCommandLaunch Launch()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(Config());
        var services = new ServiceCollection();
        services.AddSingleton(new ChatTicketRunLauncher(loader.Object, new ServerContext("c.yml"), _spawn.Object,
            NullLogger<ChatTicketRunLauncher>.Instance));
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(s => s.Create(It.IsAny<RepoConnection>())).Returns(_comments.As<ISourceProvider>().Object);
        return new PrCommandLaunch(loader.Object, new ServerContext("c.yml"), new ConfiguredRepoFinder(), _leases.Object,
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), sources.Object, NullLogger<PrCommandLaunch>.Instance);
    }

    private void Spawned(ClaimResult claim, string? runId) => _spawn.Setup(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(),
            It.IsAny<ResolvedProject>(), It.IsAny<string>(), It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()))
        .ReturnsAsync(new SpawnResult([claim], runId));

    private Task<WebhookResult> Command(string pipeline = "code") => Launch().LaunchAsync(
        new PrCommentCommand("/agent-smith fix #12", new PrCommentAuthor("https://github.com/o/api", "o/api", "alice", "alice"),
            "o/api#4", "pr:o/api#4", "4"),
        new PipelineRequest("api", pipeline, TicketId: new TicketId("12"), Headless: true), CancellationToken.None);

    [Fact]
    public async Task Admission_TicketedCommandLiveLease_CommentsLiveRunAndStartsNothing()
    {
        Spawned(ClaimResult.AlreadyClaimed(), null);
        _leases.Setup(l => l.GetByTicketAsync("api", It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaleLease("api", new TicketId("12"), "run-live", null, DateTimeOffset.UtcNow));

        (await Command()).Handled.Should().BeTrue();

        _comments.Verify(c => c.PostCommentAsync("4", It.Is<string>(t => t.Contains("run-live") && t.Contains("started nothing")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Admission_TicketedCommandFree_ClaimsOnceAndCommentsRunId()
    {
        Spawned(ClaimResult.Claimed(), "run-9");

        await Command();

        _spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(), "code",
            It.Is<IncomingTicketEnvelope>(e => e.TicketId == "12" && e.RequestedByName && e.Platform == "github"),
            It.IsAny<WebhookTriggerConfig>(), It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Once);
        _comments.Verify(c => c.PostCommentAsync("4", It.Is<string>(t => t.Contains("run-9")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Admission_SecurityScanNotLabelRouted_StillStarts()
    {
        // The envelope is marked as named by a person, so the claim does not ask whether a label routes it.
        Spawned(ClaimResult.Claimed(), "run-s");

        await Command("security-scan");

        _spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(), "security-scan",
            It.Is<IncomingTicketEnvelope>(e => e.RequestedByName), It.IsAny<WebhookTriggerConfig>(), It.IsAny<CancellationToken>(),
            It.IsAny<Dictionary<string, string>?>()), Times.Once);
    }
}
