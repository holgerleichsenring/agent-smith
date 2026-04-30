using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Providers.Tickets;

[Collection(EnvVarCollection.Name)]
public sealed class LockingTicketStatusTransitionerFactoryTests : IDisposable
{
    private static readonly string[] EnvVars =
    {
        "GITHUB_TOKEN", "GITLAB_TOKEN", "GITLAB_URL", "GITLAB_PROJECT",
        "AZURE_DEVOPS_TOKEN", "JIRA_URL", "JIRA_EMAIL", "JIRA_TOKEN"
    };

    public void Dispose()
    {
        foreach (var v in EnvVars) Environment.SetEnvironmentVariable(v, null);
    }

    [Fact]
    public void Create_Jira_ReturnsLockedDecoratorWrappingInner()
    {
        var sut = BuildSut(out _);

        var result = sut.Create(JiraConfig());

        result.Should().BeOfType<LockedTicketStatusTransitioner>();
        result.ProviderType.Should().Be("Jira");
    }

    [Fact]
    public void Create_GitHub_ReturnsInnerResultUnwrapped()
    {
        var sut = BuildSut(out _);

        var result = sut.Create(new TicketConfig { Type = "github", Url = "https://github.com/o/r" });

        result.Should().BeOfType<GitHubTicketStatusTransitioner>();
    }

    [Fact]
    public void Create_GitLab_ReturnsInnerResultUnwrapped()
    {
        var sut = BuildSut(out _);

        var result = sut.Create(new TicketConfig
        {
            Type = "gitlab", Url = "https://gitlab.com", Project = "g/p"
        });

        result.Should().BeOfType<GitLabTicketStatusTransitioner>();
    }

    [Fact]
    public void Create_AzureDevOps_ReturnsInnerResultUnwrapped()
    {
        var sut = BuildSut(out _);

        var result = sut.Create(new TicketConfig
        {
            Type = "azuredevops", Organization = "org", Project = "proj"
        });

        result.Should().BeOfType<AzureDevOpsTicketStatusTransitioner>();
    }

    [Fact]
    public void Create_UnsupportedPlatform_PropagatesNotSupportedException()
    {
        var sut = BuildSut(out _);

        var act = () => sut.Create(new TicketConfig { Type = "bitbucket" });

        act.Should().Throw<NotSupportedException>();
    }

    private static LockingTicketStatusTransitionerFactory BuildSut(out Mock<IRedisClaimLock> claimLock)
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "x");
        Environment.SetEnvironmentVariable("GITLAB_TOKEN", "x");
        Environment.SetEnvironmentVariable("GITLAB_URL", "https://gitlab.com");
        Environment.SetEnvironmentVariable("GITLAB_PROJECT", "g/p");
        Environment.SetEnvironmentVariable("AZURE_DEVOPS_TOKEN", "x");
        Environment.SetEnvironmentVariable("JIRA_URL", "https://jira.com");
        Environment.SetEnvironmentVariable("JIRA_EMAIL", "x@y");
        Environment.SetEnvironmentVariable("JIRA_TOKEN", "x");

        var inner = new TicketStatusTransitionerFactory(
            new SecretsProvider(),
            new JiraWorkflowCatalog(NullLogger<JiraWorkflowCatalog>.Instance),
            new HttpClientFactoryStub(),
            NullLoggerFactory.Instance);
        claimLock = new Mock<IRedisClaimLock>();
        return new LockingTicketStatusTransitionerFactory(
            inner, claimLock.Object, NullLoggerFactory.Instance);
    }

    private static TicketConfig JiraConfig() => new()
    {
        Type = "jira", Url = "https://jira.com", Project = "PROJ"
    };

    private sealed class HttpClientFactoryStub : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
