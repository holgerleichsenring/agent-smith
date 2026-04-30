using AgentSmith.Application;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure;
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

    private static ServiceProvider BuildCliLikeProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddAgentSmithInfrastructure();
        services.AddAgentSmithCommands();
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
