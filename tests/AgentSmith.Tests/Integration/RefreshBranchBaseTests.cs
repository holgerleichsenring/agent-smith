using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.BaseRefresh;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// 2026-11-17-023fb against real git: a refresh-base run merges the landed base into an open pull request's branch
/// and pushes it without force; a branch that moved meanwhile is skipped, never overwritten; only an agent-smith/ branch
/// is written. 2026-11-24-6811c: every merge is built and tested by the declared stages first; a conflict or a red stage
/// goes to the base-merge master, and what it cannot make whole leaves the branch as it was, said on the pull request and
/// recorded as refresh_refused.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class RefreshBranchBaseTests
{
    private const string Branch = "agent-smith/77";
    private const string PrUrl = "https://x/pull/5";
    private readonly Mock<IPrCommentProvider> _comments = new();
    private readonly List<RunEvent> _events = [];
    private readonly List<BaseMergeSituation> _passes = [];

    public RefreshBranchBaseTests() =>
        _comments.Setup(c => c.PostCommentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    [Fact]
    public async Task MovedBase_IsMergedAndPushed_SayingItWasNotBuilt()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch);
        fixture.AdvanceBase();

        var result = await RefreshAsync(fixture);

        result.Message.Should().Contain("pushed").And.Contain("not built");
        fixture.AskRemote("cat-file", "-e", $"{Branch}:{GitRemoteFixture.BaseOnlyFile}").Exit.Should().Be(0);
    }

    [Fact]
    public async Task BranchMovedMeanwhile_IsSkipped_NotOverwritten()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch);
        fixture.AdvanceBase();

        var result = await RefreshAsync(fixture, beforeRefresh: () => fixture.CommitOn(Branch, "pushed-meanwhile.txt", "x\n"));

        result.Message.Should().Contain("moved on the remote");
        fixture.AskRemote("cat-file", "-e", $"{Branch}:pushed-meanwhile.txt").Exit.Should().Be(0, "nothing is forced");
    }

    [Fact]
    public async Task AConflictTheMasterCannotResolve_LeavesTheBranch_SaysItAndRecordsTheRefusal()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch, conflicting: true);
        fixture.AdvanceBase(conflicting: true);
        var before = fixture.AskRemote("rev-parse", Branch).Output;

        var result = await RefreshAsync(fixture, master: _ => new BaseMergePass(false, "both rewrote the line", []));

        result.Message.Should().Contain("conflicts in").And.Contain(GitRemoteFixture.SharedFile).And.Contain("both rewrote the line");
        fixture.AskRemote("rev-parse", Branch).Output.Should().Be(before);
        _passes.Should().ContainSingle().Which.Unmerged.Should().Equal(GitRemoteFixture.SharedFile);
        _comments.Verify(c => c.PostCommentAsync("5", It.Is<string>(t => t.Contains(GitRemoteFixture.SharedFile)),
            It.IsAny<CancellationToken>()), Times.Once);
        _events.OfType<PullRequestOutcomeEvent>().Should().ContainSingle().Which.Should().Match<PullRequestOutcomeEvent>(e =>
            e.Status == PullRequestStatuses.RefreshRefused && e.Url == PrUrl && e.Reason!.Contains(GitRemoteFixture.SharedFile));
    }

    [Fact]
    public async Task AConflictTheMasterResolves_IsCheckedAndPushed()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch, conflicting: true);
        fixture.AdvanceBase(conflicting: true);

        var result = await RefreshAsync(fixture, stage: $"grep -q both {GitRemoteFixture.SharedFile}",
            master: _ => Write(fixture, GitRemoteFixture.SharedFile, "both sides kept\n"));

        result.Message.Should().Contain("pushed").And.Contain("1 declared stage(s) green");
        fixture.AskRemote("show", $"{Branch}:{GitRemoteFixture.SharedFile}").Output.Should().Contain("both sides kept");
        _events.Should().BeEmpty();
    }

    [Fact]
    public async Task AResolvedConflict_WithNoStageDeclared_IsRefused()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch, conflicting: true);
        fixture.AdvanceBase(conflicting: true);
        var before = fixture.AskRemote("rev-parse", Branch).Output;

        var result = await RefreshAsync(fixture, master: _ => Write(fixture, GitRemoteFixture.SharedFile, "both\n"));

        result.Message.Should().Contain("no build or test stage is declared");
        fixture.AskRemote("rev-parse", Branch).Output.Should().Be(before);
    }

    // The production case: git merged cleanly, and the merged tree no longer builds.
    [Fact]
    public async Task ACleanMergeWhoseStageIsRed_IsRepairedByTheMaster_AmendedAndPushed()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch);
        fixture.AdvanceBase();

        var result = await RefreshAsync(fixture, stage: "touch stage-output.log && test -f repaired.txt",
            master: s => s.Red is null ? new BaseMergePass(false, "no conflict expected", []) : Write(fixture, "repaired.txt", "fixed\n"));

        result.Message.Should().Contain("pushed");
        _passes.Should().ContainSingle().Which.Red!.Stage.Should().Be("test");
        fixture.AskRemote("cat-file", "-e", $"{Branch}:repaired.txt").Exit.Should().Be(0, "the repair is amended into the merge");
        fixture.AskRemote("rev-list", "--parents", "-n", "1", Branch).Output.Split(' ').Should().HaveCount(3, "it is still the merge commit");
        fixture.AskRemote("cat-file", "-e", $"{Branch}:stage-output.log").Exit.Should().NotBe(0, "a stage's output is never amended in");
    }

    [Fact]
    public async Task ARedStage_TheRepairWritingNothing_IsRefusedAfterOnePass()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch);
        fixture.AdvanceBase();
        var before = fixture.AskRemote("rev-parse", Branch).Output;

        var result = await RefreshAsync(fixture, stage: "test -f repaired.txt", master: _ => new BaseMergePass(true, "done", []));

        result.Message.Should().Contain("turns the test stage red");
        _passes.Should().ContainSingle();
        fixture.AskRemote("rev-parse", Branch).Output.Should().Be(before);
        _events.OfType<PullRequestOutcomeEvent>().Single().Status.Should().Be(PullRequestStatuses.RefreshRefused);
    }

    [Fact]
    public async Task AForeignBranch_IsRefused()
    {
        var handler = Handler(_ => new BaseMergePass(false, "-", []));

        var result = await handler.ExecuteAsync(new RefreshBranchBaseContext(
            [new RepoConnection { Name = "server" }], "feature/someone", "u", "5", "main", new PipelineContext()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("not an agent-smith work branch");
    }

    private static BaseMergePass Write(GitRemoteFixture fixture, string path, string content)
    {
        File.WriteAllText(Path.Combine(fixture.WorkPath, path), content);
        return new BaseMergePass(true, "kept both", [new CodeChange(new FilePath(path), content, "Modify")]);
    }

    private async Task<CommandResult> RefreshAsync(GitRemoteFixture fixture, Action? beforeRefresh = null, string? stage = null,
        Func<BaseMergeSituation, BaseMergePass>? master = null)
    {
        var config = new RepoConnection { Name = "server", Type = RepoType.GitHub, Url = fixture.RemotePath };
        var provider = new Mock<ISourceProvider>();
        provider.SetupGet(p => p.ProviderType).Returns("github");
        provider.Setup(p => p.CheckoutAsync(It.IsAny<BranchName?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository(new BranchName(Branch), config.Url!));
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
        ISandbox sandbox = new InProcessSandbox(jobId: "023fb", workDir: fixture.WorkPath, ownsWorkDir: false,
            InProcessStepEnvironment.Inherit, NullLogger.Instance);
        var cloner = new SandboxRepoCloner(factory.Object, AgentSmith.Tests.TestSupport.TestGitCredentials.Resolver,
            TestGit.Identity, TestGit.WorkBranchCheckout, NullLogger<SandboxRepoCloner>.Instance);
        var checkout = await cloner.CheckoutIntoSandboxesAsync(config, new RunBranch(new BranchName(Branch), false, null),
            [new KeyValuePair<string, ISandbox>("server", sandbox)], CancellationToken.None);
        checkout.Repository.Should().NotBeNull(checkout.Problem);
        beforeRefresh?.Invoke();

        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["server"] = sandbox });
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [config]);
        pipeline.Set(ContextKeys.RunId, "run-6811");
        if (stage is not null)
            pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(ContextKeys.SandboxContexts,
                new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>
                    { ["server"] = [new RemoteContextDiscovery("default", ".", "csharp", Verify: [new("test", stage)])] });
        return await Handler(master ?? (_ => new BaseMergePass(false, "not asked", []))).ExecuteAsync(
            new RefreshBranchBaseContext([config], Branch, PrUrl, "5", GitRemoteFixture.BaseBranch, pipeline)
                { Project = new ResolvedProject { Name = "p" } },
            CancellationToken.None);
    }

    private RefreshBranchBaseHandler Handler(Func<BaseMergeSituation, BaseMergePass> master)
    {
        var host = new Mock<ISourceProvider>();
        host.As<IPrCommentProvider>().Setup(c => c.PostCommentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((n, t, ct) => _comments.Object.PostCommentAsync(n, t, ct));
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(host.Object);
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((e, _) => _events.Add(e)).Returns(Task.CompletedTask);
        var stub = new Mock<IBaseMergeMaster>();
        stub.Setup(m => m.RunAsync(It.IsAny<BaseMergeSituation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BaseMergeSituation s, CancellationToken _) => { _passes.Add(s); return master(s); });
        var targets = new SandboxTargets();
        var verify = new BaseMergeVerify(new ContextVerifyStagesResolver(),
            new VerifyStageResolver(new DeclaredStagePresence(new SandboxFileReaderFactory(), NullLogger<DeclaredStagePresence>.Instance),
                NullLogger<VerifyStageResolver>.Instance),
            new VerifyCommandRunner(NullLogger<VerifyCommandRunner>.Instance), targets, TestTreeSync.TreeGit(), new BaseRefreshGit(),
            NullLogger<BaseMergeVerify>.Instance);
        return new RefreshBranchBaseHandler(new BaseRefreshMerge(TestGit.Merger, stub.Object, verify, new BaseRefreshGit()), new GitBranchPusher(),
            AgentSmith.Tests.TestSupport.TestGitCredentials.Resolver, targets, sources.Object, events.Object,
            NullLogger<RefreshBranchBaseHandler>.Instance);
    }
}
