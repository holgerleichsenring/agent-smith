using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Models;
using FluentAssertions;
using Octokit;

namespace AgentSmith.Tests.Providers.GitHub;

public sealed class GitHubRepoAddressTests
{
    [Fact]
    public void GitHubRepoAddress_GitHubCom_ApiGitHubCom()
    {
        var address = GitHubRepoAddress.Parse("https://github.com/acme/app.git");

        address.Owner.Should().Be("acme");
        address.Repo.Should().Be("app");
        address.RepoUrl.Should().Be("https://github.com/acme/app");
        address.Api.Rest.Should().Be(new Uri("https://api.github.com/"));
        address.Api.GraphQl.Should().Be(new Uri("https://api.github.com/graphql"));
    }

    [Fact]
    public void GitHubRepoAddress_EnterpriseHost_ApiV3AndApiGraphql()
    {
        var address = GitHubRepoAddress.Parse("https://ghe.example/acme/app");

        address.Api.Rest.Should().Be(new Uri("https://ghe.example/api/v3/"));
        address.Api.GraphQl.Should().Be(new Uri("https://ghe.example/api/graphql"));
        address.Api.RestRoot.Should().Be("https://ghe.example/api/v3");
    }

    [Fact]
    public void GitHubRepoAddress_NameContainingDotGit_KeepsName()
    {
        var address = GitHubRepoAddress.Parse("https://github.com/acme/acme.github.io");

        address.Repo.Should().Be("acme.github.io");
        address.FullName.Should().Be("acme/acme.github.io");
    }

    [Fact]
    public void Parse_NoRepositoryInPath_Throws()
    {
        var parse = () => GitHubRepoAddress.Parse("https://github.com/acme");

        parse.Should().Throw<ConfigurationException>();
    }

    [Fact]
    public void TryParse_NotAUrl_False()
    {
        GitHubRepoAddress.TryParse("not a url", out var address).Should().BeFalse();
        address.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://github.com")]
    [InlineData("https://api.github.com/")]
    public void For_GitHubComOrNothing_ApiGitHubCom(string? webRoot) =>
        GitHubApiEndpoints.For(webRoot).Should().Be(GitHubApiEndpoints.GitHubCom);

    [Fact]
    public void For_EnterpriseApiBase_SameEndpointsAsWebRoot() =>
        GitHubApiEndpoints.For("https://ghe.example/api/v3").Should().Be(GitHubApiEndpoints.For("https://ghe.example"));

    // Octokit keeps an enterprise REST base exactly as given, so the client and the address agree.
    [Fact]
    public void OctokitClient_EnterpriseRestBase_KeepsApiV3()
    {
        var api = GitHubApiEndpoints.For("https://ghe.example");

        new GitHubClient(new ProductHeaderValue("t"), api.Rest).Connection.BaseAddress
            .Should().Be(new Uri("https://ghe.example/api/v3/"));
    }
}
