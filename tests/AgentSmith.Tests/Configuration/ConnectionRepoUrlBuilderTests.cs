using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;
using Xunit;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// p0285: the static URL builder produces the same clone URL the discovery providers
/// return for a given repo, so exact refs resolve without a discovery round-trip.
/// </summary>
public sealed class ConnectionRepoUrlBuilderTests
{
    private readonly ConnectionRepoUrlBuilder _builder = new();

    [Fact]
    public void ConnectionRepoUrlBuilder_AzureDevOps_BuildsGitUrlFromOrgProjectName()
    {
        var conn = new ResolvedConnection
        {
            Name = "ado", Type = RepoType.AzureDevOps,
            Organization = "acme-cloud", Project = "Platform Services", Auth = "t", DefaultBranch = "develop",
        };

        var repo = _builder.Build(conn, "Service.Api", branchOverride: null);

        repo.Url.Should().Be("https://dev.azure.com/acme-cloud/Platform%20Services/_git/Service.Api");
        repo.Name.Should().Be("Service.Api");
        repo.Type.Should().Be(RepoType.AzureDevOps);
        repo.Organization.Should().Be("acme-cloud");
        repo.Project.Should().Be("Platform Services");
        repo.Auth.Should().Be("t");
        repo.DefaultBranch.Should().Be("develop");
    }

    [Fact]
    public void ConnectionRepoUrlBuilder_GitHub_BuildsOwnerRepoUrl()
    {
        var conn = new ResolvedConnection { Name = "gh", Type = RepoType.GitHub, Owner = "acme", Auth = "t" };

        var repo = _builder.Build(conn, "widgets", branchOverride: null);

        repo.Url.Should().Be("https://github.com/acme/widgets");
        repo.DefaultBranch.Should().BeNull();      // provider resolves the real default at clone time
    }

    [Fact]
    public void ConnectionRepoUrlBuilder_GitLab_UsesHostOverride()
    {
        var conn = new ResolvedConnection
        {
            Name = "gl", Type = RepoType.GitLab, Group = "team", Host = "https://gitlab.internal/", Auth = "t",
        };

        var repo = _builder.Build(conn, "svc", branchOverride: null);

        repo.Url.Should().Be("https://gitlab.internal/team/svc");
    }
}
