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
        provider.GetServices<IWebhookHandler>().Should().HaveCount(13, "all 13 webhook handlers register from a single AddWebhookHandlers call");
    }

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
        // IJobSpawner is normally registered by JobSpawnerSetup based on
        // K8s/Docker availability; mock here for the same reason.
        AddNullRedisStack(services);
        services.AddSingleton(Mock.Of<IJobSpawner>());
        services.AddCoreDispatcherServices()
                .AddServerCompositionOverrides()
                .AddSlackAdapter()
                .AddTeamsAdapter()
                .AddIntentHandlers()
                .AddWebhookHandlers()
                .AddLongRunningServices();
        services.AddJobSpawnerOptions();
        return services;
    }

    private static void AddNullRedisStack(IServiceCollection services)
    {
        services.AddSingleton(Mock.Of<IConnectionMultiplexer>());
        services.AddSingleton<IRedisJobQueue, NullRedisJobQueue>();
        services.AddSingleton(Mock.Of<IPipelineRequestStore>());
        services.AddSingleton(Mock.Of<IRedisClaimLock>());
        services.AddSingleton<IRedisLeaderLease, NullRedisLeaderLease>();
        services.AddSingleton<IJobHeartbeatService, NullJobHeartbeatService>();
        services.AddSingleton<IConversationLookup, NullConversationLookup>();
        services.AddSingleton(Mock.Of<IDialogueTransport>());
        services.AddSingleton(Mock.Of<IProgressReporter>());
    }
}
