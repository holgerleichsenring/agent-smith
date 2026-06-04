using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Lifecycle;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

public sealed class PersistWorkBranchHandlerTests
{
    private readonly Mock<ISandbox> _sandboxMock = new();
    private readonly PersistWorkBranchHandler _handler;
    private readonly List<Step> _capturedSteps = new();

    public PersistWorkBranchHandlerTests()
    {
        // Default: the repo HAS changes. p0226: `git status --porcelain` exits 0
        // with non-empty output (the change-probe); `git diff --cached --quiet`
        // exits non-zero when something is staged (p0202 pre-commit check);
        // every other step succeeds (config, add, commit, push).
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
            {
                _capturedSteps.Add(step);
                if (IsStatusCheck(step))
                    return Task.FromResult(new StepResult(
                        StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, "M Program.cs"));
                var exit = IsStagedCheck(step) ? 1 : 0;
                return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, exit, false, 0.1, null));
            });
        _handler = new PersistWorkBranchHandler(
            new SandboxGitOperations(NullLogger<SandboxGitOperations>.Instance),
            NullLogger<PersistWorkBranchHandler>.Instance);
    }

    private static bool IsStagedCheck(Step step) =>
        step.Command == "git" && step.Args is not null
        && step.Args.Contains("diff") && step.Args.Contains("--quiet");

    private static bool IsStatusCheck(Step step) =>
        step.Command == "git" && step.Args is not null
        && step.Args.Contains("status") && step.Args.Contains("--porcelain");

    [Fact]
    public async Task ExecuteAsync_NoRepositoryInPipeline_FailsWithUnknownKind()
    {
        var pipeline = NewPipelineWithSandbox();
        var ctx = NewContext(pipeline);

        var result = await _handler.ExecuteAsync(ctx, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        pipeline.Get<PersistFailureKind>(ContextKeys.PersistFailureKind)
            .Should().Be(PersistFailureKind.Unknown);
        _capturedSteps.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_NoSandboxInPipeline_FailsWithUnknownKind()
    {
        var pipeline = new PipelineContext();
        var repo = new Repository(new BranchName("agent-smith/18693"), "https://x/y.git");
        pipeline.Set(ContextKeys.Repository, repo);

        var result = await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        pipeline.Get<PersistFailureKind>(ContextKeys.PersistFailureKind)
            .Should().Be(PersistFailureKind.Unknown);
    }

    [Fact]
    public async Task ExecuteAsync_CommitAndPushSucceeds_ReturnsOkAndDoesNotSetFailureKind()
    {
        var pipeline = NewPipelineWithRepoAndSandbox();
        var ctx = NewContext(pipeline);

        var result = await _handler.ExecuteAsync(ctx, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<PersistFailureKind>(ContextKeys.PersistFailureKind, out _).Should().BeFalse();
        _capturedSteps.Should().Contain(s => s.Command == "git" && s.Args!.Contains("commit"));
        _capturedSteps.Should().Contain(s => s.Command == "git" && s.Args!.Contains("push"));
    }

    [Fact]
    public async Task ExecuteAsync_CommitMessageIncludesPipelineContextValues()
    {
        var pipeline = NewPipelineWithRepoAndSandbox();
        pipeline.Set(ContextKeys.RunId, "abc12345");
        pipeline.Set(ContextKeys.FailedStepName, "Generating plan");
        pipeline.Set(ContextKeys.ResolvedPipeline,
            new ResolvedPipelineConfig("default", new AgentConfig(), "/skills", null));

        await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        var commitStep = _capturedSteps.FirstOrDefault(s => s.Command == "git" && s.Args!.Contains("commit"));
        commitStep.Should().NotBeNull();
        var message = commitStep!.Args!.Last();
        message.Should().Contain("Run-Id: abc12345");
        message.Should().Contain("Pipeline: default");
        message.Should().Contain("Failed-Step: Generating plan");
    }

    [Fact]
    public async Task PersistWorkBranch_NothingStaged_PreCommitCheckRoutesToSkipped_NoCommitAttempted()
    {
        var pipeline = NewPipelineWithRepoAndSandbox();
        SetupNothingStaged();

        var result = await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("nothing-to-commit is a skip, not a failure");
        _capturedSteps.Should().NotContain(s => s.Args!.Contains("commit"),
            "the pre-commit check must short-circuit before any git commit runs");
    }

    [Fact]
    public async Task PersistWorkBranch_NothingStaged_AggregateOk_StepGreen()
    {
        var pipeline = NewPipelineWithRepoAndSandbox();
        SetupNothingStaged();

        var result = await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("nothing to commit");
        pipeline.TryGet<PersistFailureKind>(ContextKeys.PersistFailureKind, out _)
            .Should().BeFalse("a clean tree is not a failure kind");
    }

    [Fact]
    public async Task PersistWorkBranch_LocalisedGitSandbox_NothingStaged_StillSkipped()
    {
        // Pre-commit `git diff --cached --quiet` is exit-code based, so a
        // German-locale sandbox (where git's "nothing to commit" wording
        // changes) still classifies a clean tree deterministically. If commit
        // were ever attempted, it would return the localised message — assert
        // it is NOT attempted.
        var pipeline = NewPipelineWithRepoAndSandbox();
        SetupNothingStaged();
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.Is<Step>(st => st.Args!.Contains("commit")), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
            {
                _capturedSteps.Add(step);
                return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 1, false, 0.1,
                    "nichts zu committen, Arbeitsverzeichnis sauber"));
            });

        var result = await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _capturedSteps.Should().NotContain(s => s.Args!.Contains("commit"));
    }

    [Fact]
    public async Task PersistWorkBranch_NothingStagedAndOneRealFail_StepFailed_RealFailListed()
    {
        // Two repos: "clean" has nothing staged (skipped), "broken" has staged
        // changes but its push is auth-denied. The parent step must be red and
        // name only the genuinely-failed repo.
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Repository,
            new Repository(new BranchName("agent-smith/1"), "https://x/y.git"));
        var clean = BuildSandbox(staged: false);
        var broken = BuildSandbox(staged: true, pushExit: 128, pushError: "fatal: HTTP 401 Unauthorized");
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[]
        {
            new RepoConnection { Name = "clean", Type = RepoType.AzureDevOps, Url = "https://x/clean" },
            new RepoConnection { Name = "broken", Type = RepoType.AzureDevOps, Url = "https://x/broken" },
        });
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["clean"] = clean, ["broken"] = broken });
        var ctx = new PersistWorkBranchContext(
            pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos), new AgentConfig(), pipeline);

        var result = await _handler.ExecuteAsync(ctx, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("broken");
        result.Message.Should().NotContain("clean", "the clean repo skipped cleanly and is not a failure");
        pipeline.Get<PersistFailureKind>(ContextKeys.PersistFailureKind)
            .Should().Be(PersistFailureKind.AuthDenied);
    }

    [Fact]
    public async Task ExecuteAsync_AuthFailureOnPush_FailsWithAuthDeniedKind()
    {
        var pipeline = NewPipelineWithRepoAndSandbox();
        SetupPushFailure("HTTP 401 Unauthorized");

        var result = await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        pipeline.Get<PersistFailureKind>(ContextKeys.PersistFailureKind)
            .Should().Be(PersistFailureKind.AuthDenied);
    }

    [Fact]
    public async Task ExecuteAsync_DivergentRemoteOnPush_FailsWithRemoteDivergentKind()
    {
        var pipeline = NewPipelineWithRepoAndSandbox();
        SetupPushFailure("push rejected: non-fast-forward update");

        var result = await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        pipeline.Get<PersistFailureKind>(ContextKeys.PersistFailureKind)
            .Should().Be(PersistFailureKind.RemoteDivergent);
    }

    [Fact]
    public async Task ExecuteAsync_UnclassifiedException_FailsWithUnknownKind()
    {
        var pipeline = NewPipelineWithRepoAndSandbox();
        SetupPushFailure("disk full");

        var result = await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        pipeline.Get<PersistFailureKind>(ContextKeys.PersistFailureKind)
            .Should().Be(PersistFailureKind.Unknown);
    }

    [Fact]
    public async Task PersistWorkBranch_UntouchedRepo_ChangeProbeCannotRun_SkipsCleanly_NoConfig()
    {
        // p0226: in a multi-repo run the master edits only some repos; an
        // untouched repo's sandbox can return the -1 "couldn't run" sentinel on
        // the change-probe. That must route to NoChanges (skip), NOT a hard
        // failure on the first `git config` (the original bug: persist failed in
        // 4/5 repos with "Unknown" / exit -1).
        var pipeline = NewPipelineWithRepoAndSandbox();
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.Is<Step>(st => IsStatusCheck(st)), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
            {
                _capturedSteps.Add(step);
                return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, -1, false, 0, "could not run step"));
            });

        var result = await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("an untouched/unreachable repo is nothing to persist, not a failure");
        pipeline.TryGet<PersistFailureKind>(ContextKeys.PersistFailureKind, out _).Should().BeFalse();
        _capturedSteps.Should().NotContain(s => s.Command == "git" && s.Args!.Contains("config"),
            "no git config runs when there is nothing to persist");
    }

    private void SetupNothingStaged()
    {
        // p0226: the change-probe (`git status --porcelain`) reports a clean
        // tree — empty output — so persist skips before staging.
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.Is<Step>(st => IsStatusCheck(st)), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
            {
                _capturedSteps.Add(step);
                return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, null));
            });
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.Is<Step>(st => IsStagedCheck(st)), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
            {
                _capturedSteps.Add(step);
                return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null));
            });
    }

    private void SetupPushFailure(string errorMessage)
    {
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.Is<Step>(st => st.Args!.Contains("push")), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 128, false, 0.1, errorMessage)));
    }

    private static ISandbox BuildSandbox(bool staged, int pushExit = 0, string? pushError = null)
    {
        var mock = new Mock<ISandbox>();
        mock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
            {
                var args = step.Args ?? Array.Empty<string>();
                if (IsStatusCheck(step))
                    return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, staged ? "M f.cs" : null));
                if (IsStagedCheck(step))
                    return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, staged ? 1 : 0, false, 0.1, null));
                if (args.Contains("push"))
                    return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, pushExit, false, 0.1, pushError));
                return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null));
            });
        return mock.Object;
    }

    private PipelineContext NewPipelineWithSandbox()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandbox, _sandboxMock.Object);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [string.Empty] = _sandboxMock.Object });
        return pipeline;
    }

    private PipelineContext NewPipelineWithRepoAndSandbox()
    {
        var pipeline = NewPipelineWithSandbox();
        var repo = new Repository(
            new BranchName("agent-smith/18693"),
            "https://example.com/x/y.git");
        pipeline.Set(ContextKeys.Repository, repo);
        return pipeline;
    }

    private static PersistWorkBranchContext NewContext(PipelineContext pipeline) =>
        new(new[] { new RepoConnection { Type = RepoType.AzureDevOps, Url = "https://dev.azure.com/x/y" } },
            new AgentConfig(),
            pipeline);
}
