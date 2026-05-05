using AgentSmith.Application;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure;
using AgentSmith.Infrastructure.Extensions;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// Regression-guard for the apiscan-crash class of bug: the CLI's interactive
/// composition (no Redis) must resolve ITicketStatusTransitionerFactory and
/// create non-Jira transitioners without throwing on missing IRedisClaimLock.
/// </summary>
[Collection(EnvVarCollection.Name)]
public sealed class CliShapedDiTests : IDisposable
{
    private static readonly string[] EnvVars =
    {
        "GITHUB_TOKEN", "GITLAB_TOKEN", "GITLAB_URL", "GITLAB_PROJECT", "AZURE_DEVOPS_TOKEN"
    };

    public void Dispose()
    {
        foreach (var v in EnvVars) Environment.SetEnvironmentVariable(v, null);
    }

    [Theory]
    [InlineData("github")]
    [InlineData("gitlab")]
    [InlineData("azuredevops")]
    public void Cli_ResolvesFactoryAndCreatesNonJiraTransitioner_DoesNotThrow(string platform)
    {
        SetEnvForAllPlatforms();
        var provider = BuildCliLikeProvider();
        var factory = provider.GetRequiredService<ITicketStatusTransitionerFactory>();

        var act = () => factory.Create(ConfigFor(platform));

        act.Should().NotThrow();
    }

    [Fact]
    public void Cli_ITicketStatusTransitionerFactory_ResolvesToPlainFactory_NotLockingVariant()
    {
        var provider = BuildCliLikeProvider();

        var factory = provider.GetRequiredService<ITicketStatusTransitionerFactory>();

        factory.Should().BeOfType<TicketStatusTransitionerFactory>(
            "CLI must NOT carry the locking decorator — it has no Redis to back IRedisClaimLock");
    }

    [Fact]
    public void Cli_IRedisClaimLock_NotRegistered()
    {
        var provider = BuildCliLikeProvider();

        provider.GetService<IRedisClaimLock>().Should().BeNull(
            "CLI's interactive composition must not carry IRedisClaimLock — that's a Server concern");
    }

    [Fact]
    public void Cli_IJobHeartbeatService_NotRegistered()
    {
        var provider = BuildCliLikeProvider();

        provider.GetService<IJobHeartbeatService>().Should().BeNull(
            "CLI's interactive composition must not carry IJobHeartbeatService — only Server-side ticket lifecycle uses it");
    }

    [Fact]
    public void Cli_ITicketClaimService_NotRegistered()
    {
        var provider = BuildCliLikeProvider();

        provider.GetService<ITicketClaimService>().Should().BeNull(
            "ITicketClaimService is Server-only since p0109a — webhook + poller are its only callers");
    }

    [Fact]
    public void Cli_ResolvesPipelineExecutor_WithoutRedis_DoesNotThrow()
    {
        var provider = BuildCliLikeProvider();

        var act = () => provider.GetRequiredService<IPipelineExecutor>();

        act.Should().NotThrow(
            "regression-guard for the post-p0109 IJobHeartbeatService crash: PipelineExecutor must resolve in the CLI graph");
    }

    [Fact]
    public void Cli_PipelineLifecycleCoordinator_ResolvesToNoOp()
    {
        var provider = BuildCliLikeProvider();

        var coordinator = provider.GetRequiredService<IPipelineLifecycleCoordinator>();

        coordinator.GetType().Name.Should().Be("NoOpPipelineLifecycleCoordinator",
            "CLI default lifecycle coordinator is the no-op variant");
    }

    [Fact]
    public void Cli_RealServiceProviderFactoryBuild_ResolvesPipelineExecutor()
    {
        // Mirrors AgentSmith.Cli's actual production composition (the same call
        // that ApiScanCommand / SecurityScanCommand use). Catches DI-graph
        // regressions before they crash an end-user CLI invocation.
        using var provider = AgentSmith.Cli.ServiceProviderFactory.Build(
            verbose: false, headless: true, jobId: "", redisUrl: "");

        var act = () =>
        {
            _ = provider.GetRequiredService<IPipelineExecutor>();
            _ = provider.GetRequiredService<ITicketStatusTransitionerFactory>();
            _ = provider.GetRequiredService<IPipelineLifecycleCoordinator>();
        };

        act.Should().NotThrow(
            "the real CLI DI tree must resolve everything ApiScanCommand / SecurityScanCommand pull");
    }

    private static ServiceProvider BuildCliLikeProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddAgentSmithInfrastructure();
        services.AddAgentSmithCommands();
        services.AddInProcessSandbox();
        services.AddSingleton(Mock.Of<IDialogueTransport>());
        services.AddSingleton(Mock.Of<IProgressReporter>());
        return services.BuildServiceProvider();
    }

    private static void SetEnvForAllPlatforms()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "x");
        Environment.SetEnvironmentVariable("GITLAB_TOKEN", "x");
        Environment.SetEnvironmentVariable("GITLAB_URL", "https://gitlab.com");
        Environment.SetEnvironmentVariable("GITLAB_PROJECT", "g/p");
        Environment.SetEnvironmentVariable("AZURE_DEVOPS_TOKEN", "x");
    }

    private static TicketConfig ConfigFor(string platform) => platform switch
    {
        "github" => new TicketConfig { Type = "github", Url = "https://github.com/o/r" },
        "gitlab" => new TicketConfig { Type = "gitlab", Url = "https://gitlab.com", Project = "g/p" },
        "azuredevops" => new TicketConfig { Type = "azuredevops", Organization = "org", Project = "p" },
        _ => throw new ArgumentException($"unknown platform: {platform}")
    };
}
