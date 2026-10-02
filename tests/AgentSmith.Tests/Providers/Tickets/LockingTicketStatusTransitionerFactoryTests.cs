using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Providers.Tickets;

public sealed class LockingTicketStatusTransitionerFactoryTests
{

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

        var result = sut.Create(new TrackerConnection { Type = TrackerType.GitHub, Url = "https://github.com/o/r", Auth = "token" });

        result.Should().BeOfType<GitHubTicketStatusTransitioner>();
    }

    [Fact]
    public void Create_GitLab_ReturnsInnerResultUnwrapped()
    {
        var sut = BuildSut(out _);

        var result = sut.Create(new TrackerConnection
        {
            Type = TrackerType.GitLab, Url = "https://gitlab.com", Project = "g/p", Auth = "token"
        });

        result.Should().BeOfType<GitLabTicketStatusTransitioner>();
    }

    [Fact]
    public void Create_AzureDevOps_ReturnsInnerResultUnwrapped()
    {
        var sut = BuildSut(out _);

        var result = sut.Create(new TrackerConnection
        {
            Type = TrackerType.AzureDevOps, Organization = "org", Project = "proj", Auth = "token"
        });

        result.Should().BeOfType<AzureDevOpsTicketStatusTransitioner>();
    }

    [Fact]
    public void Create_UnsupportedPlatform_PropagatesNotSupportedException()
    {
        var sut = BuildSut(out _);

        var act = () => sut.Create(new TrackerConnection { Type = (TrackerType)999 });

        act.Should().Throw<NotSupportedException>();
    }

    private static LockingTicketStatusTransitionerFactory BuildSut(out Mock<IRedisClaimLock> claimLock)
    {
        var inner = new TicketStatusTransitionerFactory(
            TestCredentials.With(("token", "x")),
            new JiraWorkflowCatalog(NullLogger<JiraWorkflowCatalog>.Instance),
            new HttpClientFactoryStub(),
            NullLoggerFactory.Instance);
        claimLock = new Mock<IRedisClaimLock>();
        return new LockingTicketStatusTransitionerFactory(
            inner, claimLock.Object, NullLoggerFactory.Instance);
    }

    private static TrackerConnection JiraConfig() => new()
    {
        Type = TrackerType.Jira, Url = "https://jira.com", Project = "PROJ", Auth = "token", Email = "x@y"
    };

    private sealed class HttpClientFactoryStub : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
