using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-11-25-9992c: the git a run's delivery rests on — diff, verified heads, spec start heads, revert, commit,
/// status and push — names no working directory of its own, so through a run repository's sandbox every step runs
/// at /work/&lt;repo&gt;, never at the workspace root its repository used to be.
/// </summary>
public sealed class RepoSandboxGitRootTests
{
    private const string Root = "/work/api";
    private readonly StepRecordingSandbox _backend = new();
    private readonly ISandbox _repo;

    public RepoSandboxGitRootTests() => _repo = new RepoSandbox(_backend, "api");

    private void EveryGitStepRanAtTheRepositoryRoot()
    {
        _backend.Git.Should().NotBeEmpty();
        _backend.Git.Should().OnlyContain(s => s.WorkingDirectory == Root);
    }

    private PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["api"] = _repo });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>());
        return pipeline;
    }

    private static SandboxGitOperations GitOps() =>
        new(TestPushers.Plain(), TestGitCredentials.Resolver, NullLogger<SandboxGitOperations>.Instance,
            new SandboxFileReaderFactory(), new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance));

    [Fact]
    public async Task DeliveryDiff_ForBranch_DiffsAtTheRepositoryRoot()
    {
        var diff = new DeliveryDiff(
            new SandboxBaseLadder(new SandboxBaseBranch(NullLogger<SandboxBaseBranch>.Instance), NullLogger<SandboxBaseLadder>.Instance),
            new SandboxRunStartCommit(NullLogger<SandboxRunStartCommit>.Instance), NullLogger<DeliveryDiff>.Instance);

        await diff.ForBranchAsync(_repo, new DeliveryBasis("run-1", null), CancellationToken.None);

        _backend.Git.Should().Contain(s => s.Args![0] == "diff");
        EveryGitStepRanAtTheRepositoryRoot();
    }

    [Fact]
    public async Task VerifiedHeads_Record_ReadsStatusAndHeadAtTheRepositoryRoot()
    {
        var pipeline = Pipeline();

        await new VerifiedHeads(NullLogger<VerifiedHeads>.Instance)
            .RecordAsync(pipeline, new Dictionary<string, ISandbox> { ["api"] = _repo }, CancellationToken.None);

        VerifiedHeads.For(pipeline, "api").Should().Be(StepRecordingSandbox.Head);
        EveryGitStepRanAtTheRepositoryRoot();
    }

    [Fact]
    public async Task SpecStartHeads_Record_ReadsHeadAtTheRepositoryRoot()
    {
        var pipeline = Pipeline();

        await new SpecStartHeads(new SandboxTargets(), NullLogger<SpecStartHeads>.Instance)
            .RecordAsync(pipeline, CancellationToken.None);

        SpecStartHeads.For(pipeline, "api").Should().Be(StepRecordingSandbox.Head);
        EveryGitStepRanAtTheRepositoryRoot();
    }

    [Fact]
    public async Task UnverifiedWorkReverter_Restore_StagesAndComparesAtTheRepositoryRoot()
    {
        var pipeline = Pipeline();
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.VerifiedHeads,
            new Dictionary<string, string> { ["api"] = StepRecordingSandbox.Head });

        await new UnverifiedWorkReverter(GitOps(), NullLogger<UnverifiedWorkReverter>.Instance)
            .RestoreAsync(pipeline, "api", _repo, CancellationToken.None);

        _backend.Git.Should().Contain(s => s.Args!.Contains("add"));
        EveryGitStepRanAtTheRepositoryRoot();
    }

    [Fact]
    public async Task SandboxGitOperations_CommitAndStatus_RunAtTheRepositoryRoot()
    {
        var git = GitOps();

        await git.StageAllAsync(_repo, CancellationToken.None);
        await git.HasWorkingChangesAsync(_repo, CancellationToken.None);
        await git.CommitAndPushStagedAsync(_repo, "spec/x", "message",
            new RepoConnection { Name = "api", Url = "https://stub.test/api" }, CancellationToken.None);

        _backend.Git.Should().Contain(s => s.Args!.Contains("commit"));
        _backend.Git.Should().Contain(s => s.Args!.Contains("status"));
        EveryGitStepRanAtTheRepositoryRoot();
    }

    [Fact]
    public async Task GitBranchPusher_Push_PushesFromTheRepositoryRoot()
    {
        await TestPushers.Plain().PushPlainAsync(_repo, "spec/x", SandboxGitOperations.CredHelper,
            new GitCredential("token"), CancellationToken.None);

        _backend.Git.Should().Contain(s => s.Args!.Contains("push"));
        EveryGitStepRanAtTheRepositoryRoot();
    }
}
