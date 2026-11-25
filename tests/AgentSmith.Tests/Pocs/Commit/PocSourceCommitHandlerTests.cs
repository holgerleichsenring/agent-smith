using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Tests.Pocs.Scan;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Pocs.Commit;

/// <summary>
/// 2026-11-23-933dc, real git: the commit step writes a passed revision into its PoC's folder and publishes the sha; a
/// revision older than the PoC's latest is never written, and one already committed reuses its sha without a clone.
/// </summary>
public sealed class PocSourceCommitHandlerTests : IAsyncDisposable
{
    private readonly PocScanRig _scan = new();

    [Fact]
    public async Task Commit_APassedRevision_LandsInItsFolder_AndPublishesTheSha()
    {
        await using var git = await PocGitRig.CreateAsync();
        if (git is null) return;
        Passed(1);
        var context = Context(git, 1);

        var result = await Handler(git).ExecuteAsync(context, default);

        result.IsSuccess.Should().BeTrue(result.Message);
        var sha = _scan.Store.Rows[1].CommitSha!;
        (await git.OriginAsync("rev-parse", "main")).Should().Be(sha);
        context.Pipeline.Get<string>(ContextKeys.PocSourceCommitSha).Should().Be(sha);
        (await git.OriginAsync("show", "-s", "--format=%s", "main")).Should().Be("poc todo r1");
    }

    [Fact]
    public async Task Commit_ARetriedOlderRevision_ChangesNothing_AndNamesTheLatest()
    {
        await using var git = await PocGitRig.CreateAsync();
        if (git is null) return;
        Passed(1);
        Passed(2);

        var result = await Handler(git).ExecuteAsync(Context(git, 1), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("older than r2");
        git.Sandboxes.Should().BeEmpty();
    }

    [Fact]
    public async Task Commit_ARevisionWithACommitSha_ReusesItWithoutAClone()
    {
        await using var git = await PocGitRig.CreateAsync();
        if (git is null) return;
        Passed(1);
        _scan.Store.Rows[1].CommitSha = "abc123";
        var context = Context(git, 1);

        (await Handler(git).ExecuteAsync(context, default)).IsSuccess.Should().BeTrue();

        git.Sandboxes.Should().BeEmpty();
        context.Pipeline.Get<string>(ContextKeys.PocSourceCommitSha).Should().Be("abc123");
    }

    private void Passed(int revision)
    {
        _scan.Revision(revision, PocZips.App(("app/static/.gitignore", PocZips.Text("*\n"))));
        _scan.Store.Rows[revision].VerdictJson = new PocScanVerdict([], []).ToJson();
    }

    private PocSourceCommitHandler Handler(PocGitRig git) =>
        new(_scan.Store, _scan.Files(), git.Writer(), new PocsConfigSource(), NullLogger<PocSourceCommitHandler>.Instance);

    private static PocSourceCommitContext Context(PocGitRig git, int revision) =>
        new(new PipelineContext(), new ResolvedProject { Name = "poc-ann", Repos = [git.Repo] }, new PocSeeds(7, revision, "ann-sub", false, null, []));

    public ValueTask DisposeAsync() => _scan.DisposeAsync();
}
