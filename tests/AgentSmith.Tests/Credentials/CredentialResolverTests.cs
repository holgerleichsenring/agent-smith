using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Credentials;

/// <summary>2026-10-02-5f89a: a token is the entity's own auth secret, read at call time.</summary>
public sealed class CredentialResolverTests
{
    [Fact]
    public void CredentialResolver_TwoGitLabConnections_ResolveTheirOwnSecrets()
    {
        var resolver = TestCredentials.With(("gitlab_a", "token-a"), ("gitlab_b", "token-b"));

        resolver.For(new ResolvedConnection { Name = "a", Type = RepoType.GitLab, Auth = "gitlab_a" })
            .Should().Be("token-a");
        resolver.For(new ResolvedConnection { Name = "b", Type = RepoType.GitLab, Auth = "gitlab_b" })
            .Should().Be("token-b");
    }

    [Fact]
    public void For_Repo_ResolvesTheReposSecret()
    {
        var resolver = TestCredentials.With(("gh", "ghp"));

        resolver.For(new RepoConnection { Name = "r", Auth = "gh" }).Should().Be("ghp");
    }

    [Fact]
    public void For_Tracker_ResolvesTheTrackersSecret()
    {
        var resolver = TestCredentials.With(("jira", "jt"));

        resolver.For(new TrackerConnection { Name = "t", Type = TrackerType.Jira, Auth = "jira" }).Should().Be("jt");
    }

    [Fact]
    public void CredentialResolver_UnknownAuth_ThrowsMissingCredentialNamingEntityAndSecretNotAValue()
    {
        var resolver = TestCredentials.With(("other", "s3cret-value"));

        var act = () => resolver.For(new TrackerConnection { Name = "board", Auth = "missing_secret" });

        act.Should().Throw<MissingCredentialException>()
            .Which.Message.Should().Contain("Tracker 'board'").And.Contain("'missing_secret'")
            .And.NotContain("s3cret-value");
    }

    [Fact]
    public void CredentialResolver_SecretRepointedInTheStore_ResolvesTheNewValueWithoutRestart()
    {
        var values = new SwitchableSecretValues("first");
        var resolver = new CredentialResolver(values);
        var repo = new RepoConnection { Name = "r", Auth = "gitlab_token" };

        var before = resolver.For(repo);
        values.Value = "second";

        before.Should().Be("first");
        resolver.For(repo).Should().Be("second");
    }

    private sealed class SwitchableSecretValues(string value) : ISecretValues
    {
        public string Value { get; set; } = value;

        public string? Resolve(string name) => name == "gitlab_token" ? Value : null;

        public IReadOnlyList<string> All() => [Value];
    }
}
