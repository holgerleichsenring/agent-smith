using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-08-4aa9: a write that staged nothing reports the last commit ON THE SPEC
/// PATH — the value the next run's reader compares the pointer against — never HEAD,
/// which after a phase's work is a coding commit and would read as a reviewer's edit.
/// </summary>
public sealed class SpecSetWriterUnchangedTests
{
    [Fact]
    public async Task Write_NothingStaged_ReturnsTheSpecPathsLastCommit()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) => Task.FromResult(Answer(step)));

        var write = await Writer().WriteAsync(Pipeline(sandbox.Object), new RepoConnection { Name = "primary" }, Set(), default);

        write.Written.Should().BeTrue();
        write.CommitSha.Should().Be("path-sha", "the reader compares the last commit on the spec path, not HEAD");
    }

    // `diff --cached --quiet` exits 0: nothing to commit. The two sha readers answer differently.
    private static StepResult Answer(Step step)
    {
        var output = step.Args switch
        {
            ["log", "-1", "--format=%H", "--", _] => "path-sha",
            ["rev-parse", "HEAD"] => "head-sha",
            _ => string.Empty,
        };
        return new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0, null, output);
    }

    private static SpecSetWriter Writer()
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(new NullFileReader());
        return new SpecSetWriter(
            factory.Object,
            new SandboxGitOperations(
                new GitBranchPusher(), NullLogger<SandboxGitOperations>.Instance, factory.Object,
                new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance)),
            new SpecSetIndex(), new SandboxTargets(), NullLogger<SpecSetWriter>.Instance);
    }

    private static PipelineContext Pipeline(ISandbox sandbox)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["primary"] = sandbox });
        pipeline.Set(ContextKeys.Repository,
            new Repository(new BranchName("agent-smith/1"), "https://example.test/repo.git"));
        return pipeline;
    }

    private static SpecSet Set() => new(
        "azdo-1", [], SpecAccounting.Empty,
        [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
        SpecSource.Derived);

    private sealed class NullFileReader : ISandboxFileReader
    {
        public Task<bool> ExistsAsync(string path, CancellationToken ct) => Task.FromResult(false);
        public Task<string?> TryReadAsync(string path, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string> ReadRequiredAsync(string path, CancellationToken ct) => Task.FromResult(string.Empty);
        public Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);
        public Task WriteAsync(string path, string content, CancellationToken ct) => Task.CompletedTask;
    }
}
