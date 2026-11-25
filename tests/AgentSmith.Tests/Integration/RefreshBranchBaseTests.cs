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
    private readonly List<string> _followed = []; // 2026-11-24-5ee0: what the pull request got after the push
    private readonly List<RunEvent> _events = [];
    private readonly List<BaseMergeSituation> _passes = [];
    private readonly List<(string Note, bool ConcludeOnly)> _continued = [];
    private Func<string, bool, BaseMergePass> _next = (_, _) => new BaseMergePass(null, "asked: both sides contradict", []);

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
        _followed.Should().Equal(["resolve", "requeue"], "an unbuilt push is a push too (5ee0)");
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
        _followed.Should().BeEmpty("nothing was pushed (5ee0)");
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
        _followed.Should().BeEmpty("a refusal leaves the pull request as it was (5ee0)");
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
        _followed.Should().Equal(["resolve", "requeue"], "a pushed refresh resolves its refusals and queues the build (5ee0)");
        result.Message.Should().Contain("Resolved 1 earlier refusal thread(s).").And.Contain("Queued 1 required build(s)");
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

    // Review fixes: a file the master leaves untracked is not what a push carries, so the stage runs without it.
    [Fact]
    public async Task ARepairOutsideTheFileTools_IsNotBuiltOn_AndIsRefused()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch);
        fixture.AdvanceBase();

        var result = await RefreshAsync(fixture, stage: "test -f repaired.txt", master: _ =>
        {
            File.WriteAllText(Path.Combine(fixture.WorkPath, "repaired.txt"), "untracked\n");
            return new BaseMergePass(true, "wrote it with a command", []);
        });

        result.Message.Should().Contain("turns the test stage red");
    }

    [Fact]
    public async Task ARepairOfAFastForward_IsCommittedOnTop_NeverRewritingTheBase()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(workBranch: null);
        fixture.AskRemote("branch", Branch, GitRemoteFixture.BaseBranch).Exit.Should().Be(0);
        fixture.AdvanceBase();
        var baseTip = fixture.AskRemote("rev-parse", GitRemoteFixture.BaseBranch).Output.Trim();

        var result = await RefreshAsync(fixture, stage: "test -f repaired.txt",
            master: s => s.Red is null ? new BaseMergePass(false, "-", []) : Write(fixture, "repaired.txt", "fixed\n"));

        result.Message.Should().Contain("pushed");
        fixture.AskRemote("rev-parse", $"{Branch}~1").Output.Trim().Should().Be(baseTip, "the repair sits on the base's own commit");
    }

    [Fact]
    public async Task RepairPasses_AreBoundedByCiFixAttempts()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch);
        fixture.AdvanceBase();
        var n = 0;

        var result = await RefreshAsync(fixture, stage: "cat counter.txt && false",
            master: _ => Write(fixture, "counter.txt", $"{++n}\n"), fixAttempts: 2);

        result.Message.Should().Contain("red");
        _passes.Should().HaveCount(2, "each pass writes and changes the failure, so only merge.ci_fix_attempts ends it");
    }

    [Fact]
    public async Task AModifyDeleteConflict_ResolvedByRemoval_Commits()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch, conflicting: true);
        fixture.AskRemote("rm", "-q", GitRemoteFixture.SharedFile).Exit.Should().Be(0);
        fixture.AskRemote("commit", "-q", "-m", "base removed it").Exit.Should().Be(0);

        var result = await RefreshAsync(fixture, stage: $"test ! -e {GitRemoteFixture.SharedFile}", master: _ =>
        {
            File.Delete(Path.Combine(fixture.WorkPath, GitRemoteFixture.SharedFile));
            return new BaseMergePass(true, "the base removed it", []);
        });

        result.Message.Should().Contain("pushed");
        fixture.AskRemote("cat-file", "-e", $"{Branch}:{GitRemoteFixture.SharedFile}").Exit.Should().NotBe(0);
    }

    // 2026-11-24-9dc0a: the production case — the master resolved, then its loop ended without conclude.
    [Fact]
    public async Task AConflictResolvedWithoutConclude_IsCommittedCheckedAndPushed()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch, conflicting: true);
        fixture.AdvanceBase(conflicting: true);

        var result = await RefreshAsync(fixture, stage: "test -e .git",
            master: _ => Write(fixture, GitRemoteFixture.SharedFile, "both sides kept\n", whole: null));

        result.Message.Should().Contain("pushed");
        _continued.Should().BeEmpty("the server found the merge whole");
        fixture.AskRemote("show", $"{Branch}:{GitRemoteFixture.SharedFile}").Output.Should().Contain("both sides kept");
    }

    [Fact]
    public async Task APassThatResolvesNothingWithoutConclude_IsAskedOnceForTheReason_TheMarkersKeptUnmerged()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch, conflicting: true);
        fixture.AdvanceBase(conflicting: true);
        string? stages = null;
        _next = (_, only) =>
        {
            stages = GitIn(fixture.WorkPath, "ls-files", "-u", "--", GitRemoteFixture.SharedFile);
            return new BaseMergePass(null, "both rewrote the one line", []);
        };

        var result = await RefreshAsync(fixture, stage: "test -e .git", master: _ => new BaseMergePass(null, null, []));

        _continued.Should().ContainSingle().Which.ConcludeOnly.Should().BeTrue();
        stages.Should().NotBeNullOrWhiteSpace("a path left with its markers is never staged, so --ours/--theirs still work");
        result.Message.Should().Contain("conflicts in").And.Contain(GitRemoteFixture.SharedFile).And.Contain("both rewrote the one line");
        _events.OfType<PullRequestOutcomeEvent>().Single().Status.Should().Be(PullRequestStatuses.RefreshRefused);
    }

    [Fact]
    public async Task APassThatResolvesOneOfTwo_IsContinued_AndTheSecondPassIsPushed()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch, conflicting: true);
        fixture.CommitOn(Branch, "second.txt", "the branch's\n");
        fixture.CommitOn(GitRemoteFixture.BaseBranch, "second.txt", "the base's\n");
        fixture.AdvanceBase(conflicting: true);
        _next = (_, only) => only ? new BaseMergePass(false, "-", []) : Write(fixture, "second.txt", "both\n", whole: null);

        var result = await RefreshAsync(fixture, stage: "test -e .git",
            master: _ => Write(fixture, GitRemoteFixture.SharedFile, "both sides kept\n", whole: null));

        result.Message.Should().Contain("pushed");
        _continued.Should().ContainSingle().Which.Should().Match<(string Note, bool ConcludeOnly)>(c => !c.ConcludeOnly && c.Note.Contains("second.txt"));
        fixture.AskRemote("show", $"{Branch}:second.txt").Output.Should().Be("both\n");
    }

    // Review: whole=false stops even when the tree is whole.
    [Fact]
    public async Task AWholeTreeTheMasterCallsNotWhole_IsNotCommitted()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch, conflicting: true);
        fixture.AdvanceBase(conflicting: true);
        var before = fixture.AskRemote("rev-parse", Branch).Output;

        var result = await RefreshAsync(fixture, stage: "test -e .git",
            master: _ => Write(fixture, GitRemoteFixture.SharedFile, "one side\n", whole: false));

        result.Message.Should().Contain(GitRemoteFixture.SharedFile).And.Contain("kept both");
        fixture.AskRemote("rev-parse", Branch).Output.Should().Be(before);
    }

    // Review: a rename/rename conflict leaves no file at the old path; the baseline check must not resolve it before the
    // master looks, and the paths each side added stay open while nobody resolves them.
    [Fact]
    public async Task ARenameRenameConflict_IsNotResolvedBeforeTheMasterLooks_AndStaysOpen()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch);
        fixture.AskRemote("checkout", "-q", Branch).Exit.Should().Be(0);
        fixture.AskRemote("mv", GitRemoteFixture.SharedFile, "ours.txt").Exit.Should().Be(0);
        fixture.AskRemote("commit", "-q", "-m", "branch renamed it").Exit.Should().Be(0);
        fixture.AskRemote("checkout", "-q", GitRemoteFixture.BaseBranch).Exit.Should().Be(0);
        fixture.AskRemote("mv", GitRemoteFixture.SharedFile, "theirs.txt").Exit.Should().Be(0);
        fixture.AskRemote("commit", "-q", "-m", "base renamed it").Exit.Should().Be(0);
        var before = fixture.AskRemote("rev-parse", Branch).Output;
        string? stages = null;

        var result = await RefreshAsync(fixture, stage: "test -e .git", master: _ =>
        {
            stages = GitIn(fixture.WorkPath, "ls-files", "-u", "--", GitRemoteFixture.SharedFile);
            return new BaseMergePass(null, null, []);
        });

        stages.Should().NotBeNullOrWhiteSpace("the master meets the conflict as git left it");
        result.Message.Should().Contain("conflicts in").And.Contain("ours.txt").And.Contain("theirs.txt");
        fixture.AskRemote("rev-parse", Branch).Output.Should().Be(before);
    }

    [Fact]
    public async Task AWholeClaimOverMarkers_IsNotCommitted()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch, conflicting: true);
        fixture.AdvanceBase(conflicting: true);
        var before = fixture.AskRemote("rev-parse", Branch).Output;

        var result = await RefreshAsync(fixture, stage: "test -e .git", master: _ => new BaseMergePass(true, "all resolved", []));

        result.Message.Should().Contain(GitRemoteFixture.SharedFile);
        fixture.AskRemote("rev-parse", Branch).Output.Should().Be(before);
    }

    [Fact]
    public async Task AnUntouchedModifyDeleteConflict_WithoutConclude_StaysOpen()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch, conflicting: true);
        fixture.AskRemote("rm", "-q", GitRemoteFixture.SharedFile).Exit.Should().Be(0);
        fixture.AskRemote("commit", "-q", "-m", "base removed it").Exit.Should().Be(0);
        var before = fixture.AskRemote("rev-parse", Branch).Output;

        var result = await RefreshAsync(fixture, stage: "test -e .git", master: _ => new BaseMergePass(null, null, []));

        result.Message.Should().Contain("conflicts in").And.Contain(GitRemoteFixture.SharedFile);
        fixture.AskRemote("rev-parse", Branch).Output.Should().Be(before, "a side git left on disk is never taken silently");
    }

    // 2026-11-24-9dc0a: the refusal names what is still open, not the index file the union already resolved.
    [Fact]
    public async Task AUnitedMemoryIndexBesideACodeConflict_NamesOnlyTheCodePath()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        const string memory = ".agentsmith/memory/MEMORY.md";
        const string header = "# Memory index\n\n- [a](a.md) (feedback) — first\n";
        await using var fixture = GitRemoteFixture.Create(workBranch: null);
        fixture.CommitOn(GitRemoteFixture.BaseBranch, memory, header);
        fixture.CommitOn(GitRemoteFixture.BaseBranch, "Program.cs", "var a = 1;\n");
        fixture.CommitOn(Branch, memory, header + "- [b](b.md) (project) — b\n");
        fixture.CommitOn(Branch, "Program.cs", "var a = 2;\n");
        fixture.CommitOn(GitRemoteFixture.BaseBranch, memory, header + "- [c](c.md) (project) — c\n");
        fixture.CommitOn(GitRemoteFixture.BaseBranch, "Program.cs", "var a = 3;\n");

        var result = await RefreshAsync(fixture, master: _ => new BaseMergePass(false, "two values for a", []));

        result.Message.Should().Contain("Program.cs").And.NotContain("MEMORY.md");
        _passes.Single().Unmerged.Should().Equal("Program.cs");
    }

    [Fact]
    public async Task ARepairWithoutConclude_IsAmended_WithWhatTheMasterStagedByName()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        await using var fixture = GitRemoteFixture.Create(Branch);
        fixture.AdvanceBase();

        var result = await RefreshAsync(fixture, stage: "test -f repaired.txt", master: s =>
        {
            File.WriteAllText(Path.Combine(fixture.WorkPath, "staged-by-master.txt"), "x\n");
            GitIn(fixture.WorkPath, "add", "staged-by-master.txt");
            return Write(fixture, "repaired.txt", "fixed\n", whole: null);
        });

        result.Message.Should().Contain("pushed");
        fixture.AskRemote("cat-file", "-e", $"{Branch}:repaired.txt").Exit.Should().Be(0);
        fixture.AskRemote("cat-file", "-e", $"{Branch}:staged-by-master.txt").Exit.Should().Be(0, "a file the master staged by name is its repair");
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

    private static string GitIn(string dir, params string[] args)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    private static BaseMergePass Write(GitRemoteFixture fixture, string path, string content, bool? whole = true)
    {
        File.WriteAllText(Path.Combine(fixture.WorkPath, path), content);
        return new BaseMergePass(whole, "kept both", [new CodeChange(new FilePath(path), content, "Modify")]);
    }

    private async Task<CommandResult> RefreshAsync(GitRemoteFixture fixture, Action? beforeRefresh = null, string? stage = null,
        Func<BaseMergeSituation, BaseMergePass>? master = null, int fixAttempts = 3)
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
                { Project = new ResolvedProject { Name = "p", Merge = new MergePolicyConfig { CiFixAttempts = fixAttempts } } },
            CancellationToken.None);
    }

    private RefreshBranchBaseHandler Handler(Func<BaseMergeSituation, BaseMergePass> master)
    {
        var host = new Mock<ISourceProvider>();
        host.As<IPrCommentProvider>().Setup(c => c.PostCommentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((n, t, ct) => _comments.Object.PostCommentAsync(n, t, ct));
        FollowUps(host);
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(host.Object);
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((e, _) => _events.Add(e)).Returns(Task.CompletedTask);
        var stub = new Mock<IBaseMergeMaster>();
        stub.Setup(m => m.RunAsync(It.IsAny<BaseMergeSituation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BaseMergeSituation s, CancellationToken _) => { _passes.Add(s); return master(s); });
        stub.Setup(m => m.ContinueAsync(It.IsAny<BaseMergePass>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BaseMergePass _, string note, bool only, CancellationToken _) => { _continued.Add((note, only)); return _next(note, only); });
        var targets = new SandboxTargets();
        var verify = new BaseMergeVerify(new ContextVerifyStagesResolver(),
            new VerifyStageResolver(new DeclaredStagePresence(new SandboxFileReaderFactory(), NullLogger<DeclaredStagePresence>.Instance),
                NullLogger<VerifyStageResolver>.Instance),
            new VerifyCommandRunner(NullLogger<VerifyCommandRunner>.Instance), targets, new BaseRefreshGit(),
            NullLogger<BaseMergeVerify>.Instance);
        var conflicts = new BaseMergeConflictPass(stub.Object, new BaseMergeWholeness(new BaseRefreshGit()));
        return new RefreshBranchBaseHandler(new BaseRefreshMerge(TestGit.Merger, stub.Object, conflicts, verify, new BaseRefreshGit()), AgentSmith.Tests.TestSupport.TestPushers.Plain(),
            AgentSmith.Tests.TestSupport.TestGitCredentials.Resolver, targets, sources.Object, events.Object,
            new RefreshedPullRequestFollowUp(sources.Object, new PullRequestBuildRequeue(sources.Object, NullLogger<PullRequestBuildRequeue>.Instance),
                NullLogger<RefreshedPullRequestFollowUp>.Instance),
            NullLogger<RefreshBranchBaseHandler>.Instance);
    }

    // 2026-11-24-5ee0: a host with a refusal thread and a required build that did not start.
    private void FollowUps(Mock<ISourceProvider> host)
    {
        host.As<IPrMarkedThreadResolver>().Setup(r => r.ResolveMarkedThreadsAsync("5", RefreshBranchBaseHandler.Marker, It.IsAny<CancellationToken>()))
            .Callback(() => _followed.Add("resolve")).ReturnsAsync(1);
        host.As<AgentSmith.Contracts.Reviews.IPullRequestBuildRequeuer>().Setup(q => q.RequeueNotStartedAsync(PrUrl, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback(() => _followed.Add("requeue")).ReturnsAsync(1);
    }
}
