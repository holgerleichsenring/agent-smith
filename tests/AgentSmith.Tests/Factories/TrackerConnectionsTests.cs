using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Factories;

/// <summary>2026-10-02-5f89a: every connection record is read off the tracker itself.</summary>
public sealed class TrackerConnectionsTests
{
    private readonly TrackerConnections _sut = new(TestCredentials.With(("gl_a", "token-a"), ("jira", "jt")));

    [Fact]
    public void TrackerConnections_GitLabTracker_UsesItsOwnUrl()
    {
        var connection = _sut.GitLab(new TrackerConnection
        {
            Name = "gl", Type = TrackerType.GitLab, Url = "https://gitlab.example/", Project = "g/p", Auth = "gl_a",
        });

        connection.BaseUrl.Should().Be("https://gitlab.example");
        connection.ProjectPath.Should().Be("g%2Fp");
        connection.PrivateToken.Should().Be("token-a");
    }

    [Fact]
    public void Jira_TakesTheTrackersEmailAndSecret()
    {
        var connection = _sut.Jira(new TrackerConnection
        {
            Name = "j", Type = TrackerType.Jira, Url = "https://jira.example", Email = "a@b.example", Auth = "jira",
        });

        connection.Email.Should().Be("a@b.example");
        connection.ApiToken.Should().Be("jt");
    }

    [Fact]
    public void GitLab_NoProject_IsAConfigurationErrorNamingTheTracker()
    {
        var act = () => _sut.GitLab(new TrackerConnection { Name = "gl", Type = TrackerType.GitLab, Auth = "gl_a" });

        act.Should().Throw<AgentSmith.Domain.Exceptions.ConfigurationException>().WithMessage("*'gl'*project*");
    }
}
