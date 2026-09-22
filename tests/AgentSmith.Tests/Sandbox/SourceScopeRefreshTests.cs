using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-2d11b: the materialiser meets a work path that is already there. It reads who
/// the tree is a clone OF before anything else, because git refuses to clone into a non-empty
/// directory with a message the classifier reads as an unreachable host.
/// </summary>
public sealed class SourceScopeRefreshTests
{
    private const string Url = "https://stub.test/repo-a";

    private readonly SourceScopeMaterialiser _materialiser = new(new SourceScopeRefresh());

    [Fact]
    public async Task Materialiser_AWorkPathAlreadyHoldingTheRepo_IsRefreshedAndNotCloned()
    {
        var sandbox = new FakeHoldableSandbox { Origin = Url, RefreshedHead = "sha-after" };

        var sha = await _materialiser.PrepareAsync(sandbox, Repo(), revision: null, CancellationToken.None);

        sandbox.Ran("clone").Should().BeFalse("the repository is already in this work path");
        sandbox.Ran("fetch --depth 1 origin HEAD").Should().BeTrue(
            "the ref is the remote's own HEAD: the scope's clone named no branch at all");
        sandbox.Ran("reset --hard FETCH_HEAD").Should().BeTrue("a pull merges, and a merge can conflict");
        sha.Should().Be("sha-after", "the sha is re-resolved from the tree, never echoed back");
    }

    [Fact]
    public async Task Materialiser_AScopeNamingARevision_SkipsTheRefreshAndKeepsItsPin()
    {
        var sandbox = new FakeHoldableSandbox { Origin = Url, Head = "sha-pinned" };

        var sha = await _materialiser.PrepareAsync(sandbox, Repo(), "v1.4.0", CancellationToken.None);

        sandbox.Ran("fetch --depth 1 origin HEAD").Should().BeFalse(
            "a template is pinned, and the remote's HEAD would walk it off its pin");
        sandbox.Ran("reset --hard").Should().BeFalse();
        sandbox.Ran("clone").Should().BeFalse("nor is it cloned over a tree that is already there");
        sha.Should().Be("sha-pinned");
    }

    [Fact]
    public async Task Materialiser_ARefreshThatCannotReachTheHost_IsRefusedAsUnreachableNotAsAMissingRevision()
    {
        var sandbox = new FakeHoldableSandbox
        {
            Origin = Url, FailOn = "fetch", FailureText = "fatal: unable to access: could not resolve host",
        };

        var refusal = await Assert.ThrowsAsync<SourceScopeUnavailableException>(
            () => _materialiser.PrepareAsync(sandbox, Repo(), revision: null, CancellationToken.None));

        refusal.Kind.Should().Be(SourceScopeFailureKind.Unreachable);
        refusal.Message.Should().Contain("could not be refreshed");
    }

    [Fact]
    public async Task Materialiser_ARefreshTheHostRefusesTheCredentialFor_IsRefusedAsUnauthorised()
    {
        var sandbox = new FakeHoldableSandbox
        {
            Origin = Url, FailOn = "fetch", FailureText = "fatal: Authentication failed for repo-a",
        };

        var refusal = await Assert.ThrowsAsync<SourceScopeUnavailableException>(
            () => _materialiser.PrepareAsync(sandbox, Repo(), revision: null, CancellationToken.None));

        refusal.Kind.Should().Be(SourceScopeFailureKind.Unauthorised,
            "the refusals the ladder tells apart stay distinct on the new rung too");
    }

    [Fact]
    public async Task Materialiser_AnEmptyWorkPath_ClonesExactlyAsItDoesToday()
    {
        var sandbox = new FakeHoldableSandbox();

        var sha = await _materialiser.PrepareAsync(sandbox, Repo(), revision: null, CancellationToken.None);

        sandbox.Ran("clone --depth 1 --single-branch").Should().BeTrue();
        sandbox.Ran("fetch").Should().BeFalse("there was nothing to refresh");
        sha.Should().Be("sha-before");
    }

    [Fact]
    public async Task Materialiser_AWorkPathHoldingAnotherRepo_IsClonedIntoRatherThanRefreshed()
    {
        var sandbox = new FakeHoldableSandbox { Origin = "https://stub.test/somebody-else" };

        await _materialiser.PrepareAsync(sandbox, Repo(), revision: null, CancellationToken.None);

        sandbox.Ran("clone").Should().BeTrue(
            "the tree is asked who it is a clone of, so a key that pointed at the wrong "
            + "container cannot make one conversation read another's repository");
    }

    private static RepoConnection Repo() =>
        new() { Name = "repo-a", Type = RepoType.GitHub, Url = Url };
}
