using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

// p0355: the partitioner decides which repos the post-execute test/doc passes
// may touch — a repo whose sandbox working tree is clean is skipped honestly.
public sealed class RepoDiffPartitionerTests
{
    private static Mock<ISandbox> SandboxWithPorcelain(string? output)
    {
        var mock = new Mock<ISandbox>();
        mock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) => Task.FromResult(
                new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, output)));
        return mock;
    }

    private static PipelineContext Pipeline(
        (string Repo, ISandbox Sandbox)[] repos)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(
            ContextKeys.Repos, repos.Select(r => new RepoConnection { Name = r.Repo }).ToList());
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, repos.ToDictionary(r => r.Repo, r => r.Sandbox));
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.SandboxRepos, repos.ToDictionary(r => r.Repo, r => r.Repo));
        return pipeline;
    }

    private static RepoDiffPartitioner NewPartitioner() =>
        new(new SandboxGitOperations(
                NullLogger<SandboxGitOperations>.Instance, new StubSandboxFileReaderFactory()),
            NullLogger<RepoDiffPartitioner>.Instance);

    [Fact]
    public async Task PartitionAsync_EmptyDiffRepo_Skipped()
    {
        var dirty = SandboxWithPorcelain(" M src/Program.cs\n");
        var clean = SandboxWithPorcelain(null);
        var pipeline = Pipeline([("server", dirty.Object), ("client", clean.Object)]);

        var partition = await NewPartitioner().PartitionAsync(pipeline, CancellationToken.None);

        partition.ChangedRepoNames.Should().Equal("server");
        partition.SkippedRepoNames.Should().Equal("client");
        partition.ChangedSandboxes.Keys.Should().Equal("server");
    }

    [Fact]
    public async Task PartitionAsync_AllReposDirty_NoneSkipped()
    {
        var a = SandboxWithPorcelain(" M a.cs\n");
        var b = SandboxWithPorcelain("?? b.cs\n");
        var pipeline = Pipeline([("server", a.Object), ("client", b.Object)]);

        var partition = await NewPartitioner().PartitionAsync(pipeline, CancellationToken.None);

        partition.ChangedRepoNames.Should().Equal("server", "client");
        partition.SkippedRepoNames.Should().BeEmpty();
    }
}
