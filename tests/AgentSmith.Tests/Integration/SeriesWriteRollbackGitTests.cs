using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Persistence;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.Specs.MultiRepo;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// 2026-10-12-f5f7f: real git, two repositories with their own origin. A spec write into both lands
/// in both or in neither: a member that fails reverts the members this write pushed — plainly, so a
/// person's commit in between is never overwritten — every tree goes back to where it was, the revert
/// shas are recorded, and the publish step fails.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class SeriesWriteRollbackGitTests
{
    private const string Reject = "echo 'refused by the test' >&2\nexit 1";
    private const string Project = "p";

    [Fact]
    public async Task SeriesWriter_PushFailsInB_RevertsA_RecordsRevertSha_FailsRun()
    {
        await using var repos = await TwoRepoSeries.CreateAsync();
        if (repos is null) return;
        var pointers = new InMemorySpecSetPointerStore();
        (await PublishAsync(repos, pointers, Revision1())).IsSuccess.Should().BeTrue();
        var before = await repos.RemoteHeadAsync("Server");
        repos.Hook("Client", "pre-receive", Reject);

        var result = await PublishAsync(repos, pointers, Revision2());

        result.IsSuccess.Should().BeFalse("a half-written series is never worked from memory");
        result.Message.Should().Contain("Client").And.Contain("reverted in Server");
        var revert = await repos.RemoteHeadAsync("Server");
        revert.Should().NotBe(before, "Server got its revert");
        (await repos.TreeAsync("Server", revert)).Should().Be(await repos.TreeAsync("Server", before));
        var pointer = (await pointers.GetAsync(Project, SeriesRepoBranches.Key, default))!;
        pointer.RevisionNumber.Should().Be(1, "the stored revision is kept");
        pointer.ShaByRepo["Server"].Should().Be(revert);
    }

    [Fact]
    public async Task SeriesWriter_Rollback_PersonCommitInBetween_NotForced()
    {
        await using var repos = await TwoRepoSeries.CreateAsync();
        if (repos is null) return;
        repos.Hook("Server", "post-receive", PersonCommits);
        repos.Hook("Client", "pre-receive", Reject);

        var write = await WriteAsync(repos, Revision1());

        write.IsRolledBack.Should().BeTrue();
        write.RevertShas.Should().BeEmpty("the person's commit refuses our revert");
        write.Error.Should().Contain("refused in Server");
        (await repos.GitAsync("Server", "--git-dir", "../origin.git", "log", "-1", "--format=%s", TwoRepoSeries.Branch))
            .Trim().Should().Be("person", "nothing overwrote it");
    }

    [Fact]
    public async Task SeriesWriter_FailsBeforeCommitInB_TreeRestored_NothingPersisted()
    {
        await using var repos = await TwoRepoSeries.CreateAsync();
        if (repos is null) return;
        var heads = (Server: await repos.HeadAsync("Server"), Client: await repos.HeadAsync("Client"));
        repos.Hook("Client", "pre-commit", Reject, inOrigin: false);

        var write = await WriteAsync(repos, Revision1());

        write.IsRolledBack.Should().BeTrue();
        write.Pushed.Should().BeEquivalentTo(new Dictionary<string, bool> { ["Server"] = true });
        write.RevertShas.Keys.Should().Equal("Server");
        (await repos.HeadAsync("Server")).Should().Be(heads.Server);
        (await repos.HeadAsync("Client")).Should().Be(heads.Client);
        var git = SeriesDoubles.Git(repos.Files);
        (await git.HasWorkingChangesAsync(repos.Sandbox("Server"), default)).Should().BeFalse();
        (await git.HasWorkingChangesAsync(repos.Sandbox("Client"), default)).Should().BeFalse(
            "the persist after a failed run finds nothing to push");
    }

    [Fact]
    public async Task SeriesWriter_UnchangedMember_NotReverted()
    {
        await using var repos = await TwoRepoSeries.CreateAsync();
        if (repos is null) return;
        var first = Revision1();
        (await WriteAsync(repos, first)).Written.Should().BeTrue();
        var server = await repos.RemoteHeadAsync("Server");
        repos.Hook("Client", "pre-receive", Reject);
        var b = first.Specs[1];

        var write = await WriteAsync(repos, first with
        {
            Specs = [first.Specs[0], b with { Draft = b.Draft with { Yaml = b.Draft.Yaml + "# edited\n" } }],
        });

        write.IsRolledBack.Should().BeTrue();
        write.Pushed["Server"].Should().BeFalse("its sub-series did not change");
        write.RevertShas.Should().BeEmpty();
        (await repos.RemoteHeadAsync("Server")).Should().Be(server);
    }

    // Our push lands; the person's commit lands on top of it before the revert does.
    private const string PersonCommits = """
        [ -f "$GIT_DIR/person-done" ] && exit 0
        touch "$GIT_DIR/person-done"
        while read old new ref; do
          c=$(GIT_AUTHOR_NAME=p GIT_AUTHOR_EMAIL=p@example.test GIT_COMMITTER_NAME=p GIT_COMMITTER_EMAIL=p@example.test git commit-tree "$new^{tree}" -p "$new" -m person)
          git update-ref "$ref" "$c"
        done
        """;

    internal static SpecSet Revision1() => SeriesPerRepoWriteTests.Set(("a", "Server"), ("b", "Client"));

    internal static SpecSet Revision2()
    {
        var first = Revision1();
        return first with
        {
            Revisions = [.. first.Revisions, new SpecRevision(2, SpecRevisionCause.Comment, DateTimeOffset.UtcNow)],
        };
    }

    private static Task<SpecSetWriteResult> WriteAsync(TwoRepoSeries repos, SpecSet set) =>
        SeriesDoubles.Writer(repos.Files).WriteAsync(repos.Pipeline(),
            new SeriesRepos().Place(set, Scope()), default);

    private static Task<CommandResult> PublishAsync(
        TwoRepoSeries repos, ISpecSetPointerStore pointers, SpecSet set) =>
        new SpecSetPublisher(SeriesDoubles.Writer(repos.Files), new SeriesRepos(),
                new SpecSetPointerRecorder(pointers, NullLogger<SpecSetPointerRecorder>.Instance),
                Mock.Of<ISpecPullRequestOpener>(),
                new SpecRefusalReporter(Mock.Of<IEventPublisher>(), NullLogger<SpecRefusalReporter>.Instance),
                Mock.Of<IRunArtifactStore>(), NullLogger<SpecSetPublisher>.Instance)
            .PublishAsync(repos.Pipeline(), Project, Scope(), set, [], default);

    private static SeriesScope Scope() => new([TwoRepoSeries.Server, TwoRepoSeries.Client], null, [], null);
}
