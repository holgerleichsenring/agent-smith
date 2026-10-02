using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-22-b41d: one factory builds the clone for the RUN and for the read-only source
/// scope, and only the scope's was narrowed. A run diffs against a base, reads its own log
/// and can be handed a revision reachable from nothing, so its command must come out of that
/// phase byte for byte — which is what these pin.
/// </summary>
public sealed class CheckoutStepFactoryTests
{
    private static readonly RepoConnection Repo = new()
    {
        Name = "repo-a", Type = RepoType.GitHub, Url = "https://stub.test/repo-a",
    };

    private static readonly GitCredential Credential = new("token-a");

    [Fact]
    public void RunCheckout_IsUnchanged_AndStillClonesTheWholeHistory()
    {
        var args = CheckoutStepFactory.BuildCloneStep(Repo, Credential).Args!;

        args.Should().HaveCount(5, "the credential, the word, the url and the target — nothing else");
        args[0].Should().Be("-c");
        args.Skip(2).Should().Equal("clone", "https://stub.test/repo-a", ".");
    }

    [Fact]
    public void ScopeClone_AsksForOneBranchAtOneCommit()
    {
        var args = CheckoutStepFactory.BuildScopeCloneStep(Repo, Credential).Args!;

        args.Skip(2).Should().Equal(
            "clone", "--depth", "1", "--single-branch", "https://stub.test/repo-a", ".");
        args[0].Should().Be("-c", "it still talks to the remote with the credential");
    }

    [Fact]
    public void FetchRevisionAtDepth_AsksTheHostForOneRevisionAndOneCommit()
    {
        var args = CheckoutStepFactory.BuildFetchRevisionAtDepthStep(Credential, "9f1c2d").Args!;

        args.Skip(2).Should().Equal("fetch", "--depth", "1", "origin", "9f1c2d");
    }

    [Fact]
    public void FetchRevisionByName_IsUnchanged()
    {
        var args = CheckoutStepFactory.BuildFetchRevisionStep(Credential, "9f1c2d").Args!;

        // The rung the run's own rung publisher shares must not gain a depth: a depth-bounded
        // fetch into the run's full clone would make the run's tree shallow.
        args.Skip(2).Should().Equal("fetch", "origin", "9f1c2d");
    }

    // 2026-10-02-5f89g: the step carries exactly the credential it was given.
    [Fact]
    public void CheckoutStepFactory_CloneStep_CarriesTheGivenCredentialAsGitToken()
    {
        var step = CheckoutStepFactory.BuildCloneStep(Repo, new GitCredential("token-b"));

        step.Env.Should().ContainKey("GIT_TOKEN").WhoseValue.Should().Be("token-b");
    }

    [Fact]
    public void CreateRemoteBranch_NoCredential_SetsNoGitToken() =>
        CheckoutStepFactory.BuildCreateRemoteBranchStep(GitCredential.None, "HEAD", "b").Env.Should().BeNull();
}
