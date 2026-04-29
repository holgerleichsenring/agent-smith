using AgentSmith.Application.Services.RedisDisabled;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
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
        services.AddSingleton<IRedisClaimLock, NullRedisClaimLock>();
        services.AddSingleton<IRedisLeaderLease, NullRedisLeaderLease>();
        services.AddSingleton<IJobHeartbeatService, NullJobHeartbeatService>();
        services.AddSingleton<IConversationLookup, NullConversationLookup>();
        services.AddSingleton(Mock.Of<IDialogueTransport>());
        services.AddSingleton(Mock.Of<IProgressReporter>());
    }
}
