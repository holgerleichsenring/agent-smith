using AgentSmith.Infrastructure.Persistence.Extensions;
using System.Collections.Concurrent;
using AgentSmith.Application.Services.RedisDisabled;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Server;

/// <summary>
/// Regression-guard for the DI lifetime issues that bit p0106 (IPromptCatalog,
/// IRedisClaimLock, IDialogueTransport not registered; AgentProviderFactory
/// Singleton consuming Scoped IDialogueTrail). Builds Server's full DI tree
/// with ValidateOnBuild=true so any new violation fails the build.
/// </summary>
// Serialized with the other backend-env readers: SandboxBackendDetectionTests mutates
// process-global SANDBOX_TYPE / KUBERNETES_SERVICE_HOST, and a parallel BuildServerLikeServices
// here would otherwise pick the k8s backend and eagerly call InClusterConfig() → KubeConfigException.
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class ServerDiLifetimeTests
{
    [Fact]
    public void ServerDi_BuildUnderDevelopmentEnvWithValidateOnBuild_AllSingletonsResolve_NoLifetimeViolations()
    {
        var services = BuildServerLikeServices();

        var act = () => services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        act.Should().NotThrow();
    }

    [Fact]
    public void ServerDi_PromptCatalog_RedisServices_DialogueTransport_AllRegisteredFromOnePlace()
    {
        var services = BuildServerLikeServices();
        var provider = services.BuildServiceProvider();

        provider.GetService<IPromptCatalog>().Should().NotBeNull("Server DI must register IPromptCatalog");
        provider.GetService<IDialogueTransport>().Should().NotBeNull("Server DI must register IDialogueTransport");
        provider.GetService<IPipelineConfigResolver>().Should().NotBeNull("Server DI must register IPipelineConfigResolver");
        provider.GetServices<IWebhookHandler>().Should().HaveCount(20, "all 20 webhook handlers (13 ticket-event + 2 2026-10-08-e8b9c pr-review + 1 2026-10-08-f147 GitLab review + 1 2026-10-08-2123 Jira status + 3 pull-request state) register from a single AddWebhookHandlers call; 2026-10-13-0b82a removed the 3 pr-event handlers");
    }

    // 2026-10-13-0b82a: opening or pushing to a pull request is a change, not an assignment —
    // no registered handler takes the delivery, on any host, for a repository a project owns.
    [Theory]
    [InlineData("github", "pull_request", GitHubOpened)]
    [InlineData("github", "pull_request", GitHubSynchronize)]
    [InlineData("gitlab", "merge_request", GitLabOpened)]
    [InlineData("gitlab", "merge_request", GitLabPushed)]
    [InlineData("azuredevops", "git.pullrequest.created", AzureDevOpsPr)]
    [InlineData("azuredevops", "git.pullrequest.updated", AzureDevOpsPr)]
    public async Task PrOpenedDelivery_OnEveryHost_StartsNothing(string platform, string eventType, string payload)
    {
        var services = BuildServerLikeServices();
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(OwningConfig());
        services.AddSingleton(loader.Object);
        var provider = services.BuildServiceProvider();
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var handler in provider.GetServices<IWebhookHandler>().Where(h => h.CanHandle(platform, eventType)))
            (await handler.HandleAsync(payload, headers)).Handled.Should().BeFalse(handler.GetType().Name + " must not take it");
    }

    private static AgentSmithConfig OwningConfig() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["my-api"] = new()
            {
                Repos =
                [
                    new RepoConnection { Name = "gh", Url = "https://github.com/org/my-api" },
                    new RepoConnection { Name = "gl", Url = "https://gitlab.com/org/my-api" },
                    new RepoConnection { Name = "ado", Url = "https://dev.azure.com/org/proj/_git/my-api" },
                ],
            },
        },
    };

    private const string GitHubOpened = """
        { "action": "opened", "pull_request": { "number": 42, "head": { "sha": "h", "ref": "feature/x" }, "base": { "sha": "b" },
          "user": { "login": "dev-a" }, "labels": [] },
          "repository": { "full_name": "org/my-api", "clone_url": "https://github.com/org/my-api.git" } }
        """;

    private const string GitHubSynchronize = """
        { "action": "synchronize", "pull_request": { "number": 42, "head": { "sha": "h2", "ref": "feature/x" }, "base": { "sha": "b" },
          "user": { "login": "dev-a" }, "labels": [] },
          "repository": { "full_name": "org/my-api", "clone_url": "https://github.com/org/my-api.git" } }
        """;

    private const string GitLabOpened = """
        { "user": { "username": "dev-a" }, "object_attributes": { "iid": 7, "action": "open", "source_branch": "feature/x",
          "last_commit": { "id": "h" } }, "labels": [],
          "project": { "path_with_namespace": "org/my-api", "web_url": "https://gitlab.com/org/my-api" } }
        """;

    private const string GitLabPushed = """
        { "user": { "username": "dev-a" }, "object_attributes": { "iid": 7, "action": "update", "source_branch": "feature/x",
          "last_commit": { "id": "h2" }, "oldrev": "h" }, "labels": [],
          "project": { "path_with_namespace": "org/my-api", "web_url": "https://gitlab.com/org/my-api" } }
        """;

    private const string AzureDevOpsPr = """
        { "resource": { "pullRequestId": 11, "status": "active", "sourceRefName": "refs/heads/feature/x",
          "createdBy": { "uniqueName": "dev-a@example.test" }, "lastMergeSourceCommit": { "commitId": "h" },
          "lastMergeTargetCommit": { "commitId": "b" },
          "repository": { "name": "my-api", "remoteUrl": "https://org@dev.azure.com/org/proj/_git/my-api" } } }
        """;

    [Fact]
    public void ServerDi_TicketStatusTransitionerFactory_ResolvesToLockingVariant()
    {
        var services = BuildServerLikeServices();
        var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<ITicketStatusTransitionerFactory>();

        factory.Should().BeOfType<LockingTicketStatusTransitionerFactory>(
            "Server's AddServerCompositionOverrides must override Infrastructure's plain binding");
    }

    [Fact]
    public void ServerDi_PipelineLifecycleCoordinator_ResolvesToTicketAwareVariant()
    {
        var services = BuildServerLikeServices();
        var provider = services.BuildServiceProvider();

        var coordinator = provider.GetRequiredService<IPipelineLifecycleCoordinator>();

        coordinator.GetType().Name.Should().Be("TicketAwarePipelineLifecycleCoordinator",
            "Server's AddServerCompositionOverrides must rebind to the ticket-aware coordinator");
    }

    [Fact]
    public void ServerDi_TicketClaimService_IsRegistered()
    {
        var services = BuildServerLikeServices();
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetService<ITicketClaimService>().Should().NotBeNull(
            "Server-side composition registers ITicketClaimService (not Application — see p0109a)");
    }

    // p0489: the manual init launcher is reached only through the HTTP endpoint, so a
    // wiring break would surface as a 500 in production and nowhere in a mocked unit
    // test. Resolve it from the REAL server composition, in a request scope.
    [Fact]
    public void ServerDi_InitRunLauncher_ResolvesFromTheRealComposition()
    {
        // The launch composes with the dashboard API it belongs to — the same gate
        // decides whether the endpoint exists and whether its services do.
        var services = BuildServerLikeServices().AddDashboardApi();
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetService<AgentSmith.Server.Services.Init.InitRunLauncher>()
            .Should().NotBeNull("POST /api/projects/{name}/init resolves the launcher per request");
    }

    // 2026-10-13-9a59l: an approval in a ticket's own conversation and its preview resolve from the real
    // composition — with the dashboard API, and without it, where a chat conversation still approves.
    [Fact]
    public void ServerDi_TicketContinuation_ResolvesFromTheRealComposition()
    {
        foreach (var services in new[] { BuildServerLikeServices(), BuildServerLikeServices().AddDashboardApi() })
        {
            using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            using var scope = provider.CreateScope();

            scope.ServiceProvider.GetRequiredService<AgentSmith.Server.Services.SpecDialog.IOutcomeSink>()
                .Should().BeOfType<AgentSmith.Server.Services.SpecDialog.TicketFilingOutcomeSink>();
            scope.ServiceProvider.GetRequiredService<AgentSmith.Server.Services.SpecDialog.Continuation.TicketContinuationAct>()
                .Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<AgentSmith.Server.Services.Tickets.TicketApprovalPreview>()
                .Should().NotBeNull();
        }
    }

    // 2026-10-13-9a59n: Retry resolves from the real composition with the dashboard API that maps its route.
    [Fact]
    public void ServerDi_RunRetry_ResolvesFromTheRealComposition()
    {
        using var provider = AgentSmith.Infrastructure.Persistence.Extensions.ServerStateExtensions.AddServerState(BuildServerLikeServices()).AddDashboardApi()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<AgentSmith.Server.Services.Rework.RunRetry>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Infrastructure.Persistence.Repositories.RunRetryRequestRepository>()
            .Should().NotBeNull();
    }

    // 2026-10-13-9a59t: the closed ticket's follow-up is a worker singleton; what it files with is scoped and
    // resolves inside the one scope it opens per call.
    [Fact]
    public void ClosedTicketActFollowUp_ResolvesScopedStarterAndLinkInOneScope()
    {
        var services = AgentSmith.Infrastructure.Persistence.Extensions.ServerStateExtensions.AddServerState(BuildServerLikeServices());
        services.AddReworkNudgeWorker();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        provider.GetRequiredService<AgentSmith.Server.Services.Rework.ClosedTicketActFollowUp>().Should().NotBeNull();
        provider.GetRequiredService<AgentSmith.Server.Services.Rework.ReworkNudgeHandler>().Should().NotBeNull();
        using var scope = provider.CreateScope();
        var filing = AgentSmith.Server.Services.Rework.FollowUpServices.Of(scope.ServiceProvider);
        filing.Starter.Should().NotBeNull();
        filing.Link.Should().NotBeNull();
    }

    // 2026-10-13-8762v: the series start is a worker singleton; the runway reader it asks resolves in its own scope.
    [Fact]
    public void ServerDi_SeriesAutoStart_ResolvesWithItsRunwayReaderInAScope()
    {
        var services = AgentSmith.Infrastructure.Persistence.Extensions.ServerStateExtensions.AddServerState(BuildServerLikeServices())
            .AddDashboardApi();
        services.AddReworkNudgeWorker();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        provider.GetRequiredService<AgentSmith.Server.Contracts.ISeriesAutoStart>().Should()
            .BeOfType<AgentSmith.Server.Services.Rework.SeriesAutoStart>();
        provider.GetRequiredService<AgentSmith.Server.Services.Rework.ReworkNoActTail>().Should().NotBeNull();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Server.Services.Tickets.Runway.SeriesRunwayReader>().Should().NotBeNull();
    }

    // 2026-10-13-9a59o: a bound turn's run record reads its facts through the real composition.
    [Fact]
    public void ServerDi_BoundTicketRunFacts_ResolveFromTheRealComposition()
    {
        using var provider = AgentSmith.Infrastructure.Persistence.Extensions.ServerStateExtensions.AddServerState(BuildServerLikeServices())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<AgentSmith.Server.Services.SpecDialog.BoundTicketReaders>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Server.Services.SpecDialog.TicketRunsFacts>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Contracts.Reviews.IPrReviewRecordReader>().Should().NotBeNull();
    }

    // 2026-10-14-da5ed: a bound turn's start port and what it asks through resolve from the real composition.
    [Fact]
    public void ServerDi_ConversationAttemptStart_ResolvesFromTheRealComposition()
    {
        using var provider = BuildServerLikeServices().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<AgentSmith.Server.Services.SpecDialog.BoundAttemptStarts>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Server.Services.SpecDialog.Continuation.PendingSeriesEditReader>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Server.Services.SpecDialog.Continuation.PendingEditStart>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Server.Services.Rework.RunRetry>().Should().NotBeNull();
    }

    [Fact]
    public async Task ServerShapedDi_ConcurrentJiraTransitions_SecondGetsPreconditionFailed()
    {
        // Real concurrency through the Server-shape lock decorator: the inner
        // transitioner gates inside TransitionAsync so we can launch two parallel
        // calls and observe the second hit a held lock.
        var lockImpl = new InMemoryClaimLock();
        var gated = new GatedTransitioner();
        var sut = new LockedTicketStatusTransitioner(
            gated, lockImpl,
            NullLogger<LockedTicketStatusTransitioner>.Instance);

        var ticket = new TicketId("PROJ-1");
        var first = sut.TransitionAsync(ticket,
            TicketLifecycleStatus.Pending, TicketLifecycleStatus.Enqueued,
            CancellationToken.None);
        await gated.AcquiredSignal.Task;

        var second = await sut.TransitionAsync(ticket,
            TicketLifecycleStatus.Pending, TicketLifecycleStatus.Enqueued,
            CancellationToken.None);

        second.Outcome.Should().Be(TransitionOutcome.PreconditionFailed,
            "second concurrent transition must observe the label-lock held by the first");
        gated.Release();
        var firstResult = await first;
        firstResult.IsSuccess.Should().BeTrue();
    }

    private sealed class InMemoryClaimLock : IRedisClaimLock
    {
        private readonly ConcurrentDictionary<string, string> _holders = new();

        public Task<string?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct)
        {
            var token = Guid.NewGuid().ToString("N");
            return Task.FromResult(_holders.TryAdd(key, token) ? token : null);
        }

        public Task ReleaseAsync(string key, string token, CancellationToken ct)
        {
            if (_holders.TryGetValue(key, out var held) && held == token)
                _holders.TryRemove(new KeyValuePair<string, string>(key, held));
            return Task.CompletedTask;
        }
    }

    private sealed class GatedTransitioner : ITicketStatusTransitioner
    {
        public TaskCompletionSource AcquiredSignal { get; } = new();
        private readonly TaskCompletionSource _gate = new();
        public string ProviderType => "Jira";

        public async Task<TransitionResult> TransitionAsync(
            TicketId ticketId, TicketLifecycleStatus from,
            TicketLifecycleStatus to, CancellationToken ct)
        {
            AcquiredSignal.TrySetResult();
            await _gate.Task.WaitAsync(ct);
            return TransitionResult.Succeeded();
        }

        public Task<TicketLifecycleStatus?> ReadCurrentAsync(TicketId ticketId, CancellationToken ct)
            => Task.FromResult<TicketLifecycleStatus?>(null);

        public void Release() => _gate.TrySetResult();
    }

    private static IServiceCollection BuildServerLikeServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton(new ServerContext("/tmp/agentsmith.yml"));

        // Mirror Server's Program.cs registration. AddRedis is replaced by
        // null-fakes so we don't need a live Redis to validate the DI tree.
        AddNullRedisStack(services);
        var configuration = new ConfigurationBuilder().Build();
        services.AddCoreDispatcherServices()
                .AddServerCompositionOverrides()
                // p0315a: production always wires the relational store (p0246g);
                // mirroring it here keeps scoped repo chains (spec-dialog) valid.
                .AddRelationalPersistence()
                // 2026-10-14-da5ed: as production does — a conversation's start asks through RunRetry's store.
                .AddServerState()
                // 2026-10-13-8762u: the access store's admin route — the stored series reader reads the config store.
                .AddCallerIdentity()
                .AddSandbox()
                .AddSandboxOptions(configuration)
                .AddSandboxGlobalConfig()
                // 2026-09-15-9033: the dashboard channel composes here too — this mirror is
                // where a lifetime violation would show.
                .AddDashboardAdapter()
                .AddSlackAdapter()
                .AddTeamsAdapter()
                .AddIntentHandlers()
                .AddWebhookHandlers()
                .AddLongRunningServices();
        return services;
    }

    private static void AddNullRedisStack(IServiceCollection services)
    {
        services.AddSingleton(Mock.Of<IConnectionMultiplexer>());
        services.AddSingleton<IRedisJobQueue, NullRedisJobQueue>();
        services.AddSingleton(Mock.Of<IRedisClaimLock>());
        services.AddSingleton<IRedisLeaderLease, NullRedisLeaderLease>();
        services.AddSingleton(Mock.Of<IDialogueTransport>());
        services.AddSingleton(Mock.Of<IProgressReporter>());
    }
}
