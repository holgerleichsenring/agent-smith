using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0315b: the lazy read-only source sandbox. Nothing spawns until the first
/// content read; Run/WriteFile are refused in the step vocabulary itself;
/// materialisation clones once and is idempotent; disposal tears down only
/// what was materialised.
/// </summary>
public sealed class SourceScopeSandboxTests
{
    private static readonly ResolvedProject Project = new() { Name = "p" };

    [Fact]
    public async Task RunStepAsync_RunOrWriteStep_RefusedWithoutMaterializing()
    {
        var factory = new StubSandboxFactory();
        var sut = Build(factory, RepoWithUrl());

        var run = await sut.RunStepAsync(Step(StepKind.Run), null, CancellationToken.None);
        var write = await sut.RunStepAsync(Step(StepKind.WriteFile), null, CancellationToken.None);

        run.ExitCode.Should().NotBe(0);
        run.ErrorMessage.Should().Contain("read-only");
        write.ExitCode.Should().NotBe(0);
        factory.Spawned.Should().BeEmpty("a refused step must not spawn anything");
        sut.IsMaterialized.Should().BeFalse();
    }

    [Fact]
    public async Task RunStepAsync_RepoWithoutUrl_RefusedWithGuidance()
    {
        var factory = new StubSandboxFactory();
        var sut = Build(factory, new RepoConnection { Name = "local-only", Type = RepoType.Local });

        var result = await sut.RunStepAsync(Step(StepKind.ReadFile), null, CancellationToken.None);

        result.ExitCode.Should().NotBe(0);
        result.ErrorMessage.Should().Contain("no clone URL");
        factory.Spawned.Should().BeEmpty();
    }

    [Fact]
    public async Task RunStepAsync_FirstRead_MaterializesOnce_ClonesThenDelegates()
    {
        var factory = new StubSandboxFactory();
        var sut = Build(factory, RepoWithUrl());

        var first = await sut.RunStepAsync(Step(StepKind.ReadFile), null, CancellationToken.None);
        var second = await sut.RunStepAsync(Step(StepKind.Grep), null, CancellationToken.None);

        first.ExitCode.Should().Be(0);
        second.ExitCode.Should().Be(0);
        factory.Spawned.Should().HaveCount(1, "materialisation happens exactly once");
        factory.Spawned[0].Spec.ToolchainImage.Should().Be("buildpack-deps:bookworm-scm");
        factory.Spawned[0].Spec.RunId.Should().Be("run-1", "the reaper must see the active run's label");
        var kinds = factory.Spawned[0].Sandbox.RanSteps.Select(s => s.Kind).ToList();
        kinds.Should().Equal(StepKind.Run, StepKind.ReadFile, StepKind.Grep);
        factory.Spawned[0].Sandbox.RanSteps[0].Command.Should().Be("git");
        sut.IsMaterialized.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_NeverMaterialized_SpawnsNothing()
    {
        var factory = new StubSandboxFactory();
        var sut = Build(factory, RepoWithUrl());

        await sut.DisposeAsync();

        factory.Spawned.Should().BeEmpty();
    }

    private static SourceScopeSandbox Build(StubSandboxFactory factory, RepoConnection repo)
    {
        var specBuilder = new SandboxSpecBuilder(
            new StubSandboxResourceResolver(),
            Mock.Of<Application.Services.Sandbox.IAgentImageResolver>(
                r => r.Resolve(It.IsAny<ResolvedProject>()) == "agent:test"));
        var runContext = new Mock<IRunContextAccessor>();
        runContext.SetupGet(r => r.CurrentRunId).Returns("run-1");
        return new SourceScopeSandbox(
            Project, repo, factory, specBuilder, runContext.Object,
            NullLogger<SourceScopeSandbox>.Instance);
    }

    private static RepoConnection RepoWithUrl() => new()
    {
        Name = "repo-a", Type = RepoType.GitHub, Url = "https://stub.test/repo-a",
    };

    private static Step Step(StepKind kind) => new(
        AgentSmith.Sandbox.Wire.Step.CurrentSchemaVersion, Guid.NewGuid(), kind,
        Command: kind == StepKind.Run ? "echo" : null,
        Path: kind is StepKind.ReadFile or StepKind.Grep ? "/work/file.cs" : null,
        Pattern: kind == StepKind.Grep ? "x" : null,
        Content: kind == StepKind.WriteFile ? "content" : null);
}
