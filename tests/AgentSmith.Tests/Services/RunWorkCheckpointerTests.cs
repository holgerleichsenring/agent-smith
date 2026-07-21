using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

// p0360: mid-run checkpoint pushes — a dying run loses at most the work since
// the last checkpoint. These pin the contract: dirty tree → secret-scanned
// commit+push to the run branch + CheckpointedRepos marking; throttled by the
// min interval; a leak blocks the push; disabled interval is a no-op.
public sealed class RunWorkCheckpointerTests
{
    private readonly List<Step> _steps = new();
    private readonly Mock<ISecretPatternScanner> _scanner = new();

    private RunWorkCheckpointer BuildCheckpointer() => new(
        new SandboxGitOperations(NullLogger<SandboxGitOperations>.Instance, new StubSandboxFileReaderFactory()),
        _scanner.Object,
        NullLogger<RunWorkCheckpointer>.Instance);

    private ISandbox BuildSandbox(bool dirty = true)
    {
        var mock = new Mock<ISandbox>();
        mock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
            {
                _steps.Add(step);
                var args = step.Args ?? Array.Empty<string>();
                if (args.Contains("status") && args.Contains("--porcelain"))
                    return Task.FromResult(Result(step, 0, dirty ? "M src/Program.cs" : ""));
                if (args.Contains("--quiet"))
                    return Task.FromResult(Result(step, dirty ? 1 : 0, null));
                if (args.Contains("--name-only"))
                    return Task.FromResult(Result(step, 0, "src/Program.cs"));
                if (args.Contains("--no-color"))
                    return Task.FromResult(Result(step, 0, "+ real diff"));
                return Task.FromResult(Result(step, 0, null));
            });
        return mock.Object;
    }

    private static StepResult Result(Step step, int exit, string? output) =>
        new(StepResult.CurrentSchemaVersion, step.StepId, exit, false, 0.1, null, output);

    private PipelineContext BuildPipeline(ISandbox sandbox)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("agent-smith/19106"), "https://x/y.git"));
        pipeline.Set(ContextKeys.RunId, "run-1");
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[]
        {
            new RepoConnection { Name = "server", Type = RepoType.AzureDevOps, Url = "https://x/server" },
        });
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["server"] = sandbox });
        return pipeline;
    }

    [Fact]
    public async Task Checkpoint_DirtyRepo_CommitsAndPushesToRunBranch_AndMarksRepo()
    {
        _scanner.Setup(s => s.Scan(It.IsAny<string>(), It.IsAny<string>())).Returns([]);
        var pipeline = BuildPipeline(BuildSandbox(dirty: true));

        await BuildCheckpointer().CheckpointAsync(pipeline, 120, CancellationToken.None);

        _steps.Should().Contain(s =>
            s.Args!.Contains("commit") && s.Args!.Any(a => a.StartsWith("[checkpoint] agent-smith run run-1")));
        _steps.Should().Contain(s =>
            s.Args!.Contains("push") && s.Args!.Contains("HEAD:agent-smith/19106"));
        RunWorkCheckpointer.HasCheckpointedCode(pipeline, "server").Should().BeTrue();
        RunWorkCheckpointer.WasCheckpointed(pipeline, "server").Should().BeTrue();
    }

    [Fact]
    public async Task Checkpoint_WithinMinInterval_SecondCallIsThrottled()
    {
        _scanner.Setup(s => s.Scan(It.IsAny<string>(), It.IsAny<string>())).Returns([]);
        var pipeline = BuildPipeline(BuildSandbox(dirty: true));
        var checkpointer = BuildCheckpointer();

        await checkpointer.CheckpointAsync(pipeline, 3600, CancellationToken.None);
        var stepsAfterFirst = _steps.Count;
        await checkpointer.CheckpointAsync(pipeline, 3600, CancellationToken.None);

        _steps.Count.Should().Be(stepsAfterFirst, "the second checkpoint inside the interval must not touch git");
    }

    [Fact]
    public async Task Checkpoint_SecretPatternInDiff_NotPushedNotMarked()
    {
        _scanner.Setup(s => s.Scan(It.IsAny<string>(), It.IsAny<string>()))
            .Returns([new SecretMatch("staged", 3, "aws-key")]);
        var pipeline = BuildPipeline(BuildSandbox(dirty: true));

        await BuildCheckpointer().CheckpointAsync(pipeline, 120, CancellationToken.None);

        _steps.Should().NotContain(s => s.Args!.Contains("push"));
        RunWorkCheckpointer.WasCheckpointed(pipeline, "server").Should().BeFalse();
    }

    [Fact]
    public async Task Checkpoint_CleanTree_NoCommitNoMark()
    {
        _scanner.Setup(s => s.Scan(It.IsAny<string>(), It.IsAny<string>())).Returns([]);
        var pipeline = BuildPipeline(BuildSandbox(dirty: false));

        await BuildCheckpointer().CheckpointAsync(pipeline, 120, CancellationToken.None);

        _steps.Should().NotContain(s => s.Args!.Contains("commit"));
        RunWorkCheckpointer.WasCheckpointed(pipeline, "server").Should().BeFalse();
    }

    [Fact]
    public async Task Checkpoint_DisabledInterval_DoesNothing()
    {
        var pipeline = BuildPipeline(BuildSandbox(dirty: true));

        await BuildCheckpointer().CheckpointAsync(pipeline, 0, CancellationToken.None);

        _steps.Should().BeEmpty();
    }
}
