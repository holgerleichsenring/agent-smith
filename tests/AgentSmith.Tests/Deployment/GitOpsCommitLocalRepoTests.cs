using AgentSmith.Application.Services.Deployment;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.Providers.Source.BranchWrites;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Deployment;

/// <summary>
/// 2026-11-23-c81ea: commit mode end to end on a LOCAL repository through the real source provider factory — writes
/// and deletes land as ONE commit on the default branch, and the same delete retried after its commit landed is
/// committed with nothing to commit.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class GitOpsCommitLocalRepoTests : IDisposable
{
    private readonly LocalGitRepo _repo = new();

    public GitOpsCommitLocalRepoTests()
    {
        _repo.Commit("deploy/poc/old.yaml", "old");
        _repo.Commit("src/poc/app.cs", "code");
    }

    public void Dispose() => _repo.Dispose();

    [Fact]
    public async Task CommitAsync_WritesAndDeletes_OneCommitOnTheDefaultBranch()
    {
        var before = _repo.Git("rev-parse", "main");

        var (committed, sha, _) = await Commit([new RepoFile("deploy/poc/app.yaml", "kind: Deployment"),
            RepoFile.Deletion("deploy/poc/old.yaml")]);

        committed.Should().BeTrue();
        _repo.Git("rev-parse", "main").Should().Be(sha);
        _repo.Git("rev-parse", "main~1").Should().Be(before, "one commit carries both");
        _repo.Git("ls-tree", "-r", "--name-only", "main").Split('\n')
            .Should().BeEquivalentTo("deploy/poc/app.yaml", "src/poc/app.cs");
    }

    [Fact]
    public async Task CommitAsync_RetriedDeleteThatAlreadyLanded_Succeeds()
    {
        (await Commit([RepoFile.Deletion("deploy/poc/old.yaml")])).Committed.Should().BeTrue();
        var head = _repo.Git("rev-parse", "main");

        var (committed, sha, sentence) = await Commit([RepoFile.Deletion("deploy/poc/old.yaml")]);

        committed.Should().BeTrue(sentence);
        sha.Should().BeNull();
        sentence.Should().Contain("already holds it");
        _repo.Git("rev-parse", "main").Should().Be(head);
    }

    private Task<(bool Committed, string? Sha, string Sentence)> Commit(IReadOnlyList<RepoFile> files)
    {
        var factory = new SourceProviderFactory(Mock.Of<ICredentialResolver>(), Mock.Of<IHttpClientFactory>(),
            Mock.Of<IGitHubClientFactory>(), Mock.Of<IAzDoClientFactory>(), NullLoggerFactory.Instance);
        var repo = new RepoConnection { Name = "gitops", Type = RepoType.Local, Path = _repo.Path, DefaultBranch = "main" };
        return new GitOpsDelivery(factory, new DeployCiStatus(NullLogger<DeployCiStatus>.Instance))
            .CommitAsync(new GitOpsCommitOrder(repo, files, "poc: deploy"), _ => Task.FromResult<GitOpsCommitOrder?>(null),
                3, CancellationToken.None);
    }
}
