using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0315b: the lazy read-only source sandbox. Nothing spawns until the first step is
/// served; materialisation clones once and is idempotent; disposal tears down only what was
/// materialised.
/// <para>2026-09-22-46ef: what READ-ONLY means is pinned here. A process is served when its
/// program is one the server itself builds and refused otherwise — so the shell a
/// model-authored command travels in never runs — and a write is served only under a prefix
/// the scope declares, of which there are none by default.</para>
/// </summary>
public sealed class SourceScopeSandboxTests
{
    private static readonly ResolvedProject Project = new() { Name = "p" };

    [Fact]
    public async Task SourceScope_AProcessStepInvokingAShell_IsRefusedAndSaysSo()
    {
        var factory = new StubSandboxFactory();
        var sut = Build(factory, RepoWithUrl());

        var run = await sut.RunStepAsync(
            Process("/bin/sh", "-c", "rm -rf /work"), null, CancellationToken.None);

        run.ExitCode.Should().NotBe(0);
        run.ErrorMessage.Should().Contain("/bin/sh", "the refusal names what it refused");
        factory.Spawned.Should().BeEmpty("a refused step must not spawn anything");
        sut.IsMaterialized.Should().BeFalse();
    }

    [Fact]
    public async Task SourceScope_AProgramOutsideTheAllowance_IsRefused()
    {
        var factory = new StubSandboxFactory();
        var sut = Build(factory, RepoWithUrl());

        var run = await sut.RunStepAsync(
            Process("python3", "-c", "import os"), null, CancellationToken.None);

        run.ExitCode.Should().NotBe(0);
        run.ErrorMessage.Should().Contain("python3");
        factory.Spawned.Should().BeEmpty();
    }

    [Fact]
    public async Task SourceScope_AProcessStepNamingAProgram_IsServed()
    {
        var factory = new StubSandboxFactory();
        var sut = Build(factory, RepoWithUrl());

        var run = await sut.RunStepAsync(
            Process("find", "/work", "-type", "f"), null, CancellationToken.None);

        run.ExitCode.Should().Be(0);
        factory.Spawned[0].Sandbox.RanSteps
            .Should().Contain(step => step.Command == "find", "the scope serves what the server builds");
    }

    [Fact]
    public async Task SourceScope_AGrep_StillSpawnsItsEngine()
    {
        var factory = new StubSandboxFactory();
        var sut = Build(factory, RepoWithUrl());

        var grep = await sut.RunStepAsync(Step(StepKind.Grep), null, CancellationToken.None);

        grep.ExitCode.Should().Be(0);
        factory.Spawned[0].Sandbox.RanSteps
            .Should().Contain(step => step.Kind == StepKind.Grep);
    }

    [Fact]
    public async Task SourceScope_NoPrefixDeclared_RefusesEveryWrite()
    {
        var factory = new StubSandboxFactory();
        var sut = Build(factory, RepoWithUrl());

        var write = await sut.RunStepAsync(
            Write(".agentsmith/specs/a.yaml"), null, CancellationToken.None);

        write.ExitCode.Should().NotBe(0);
        write.ErrorMessage.Should().Contain("no writable prefix");
        factory.Spawned.Should().BeEmpty("a refused write must not spawn anything");
    }

    [Fact]
    public async Task SourceScope_AWriteUnderADeclaredPrefix_IsServed()
    {
        var factory = new StubSandboxFactory();
        var sut = Build(factory, RepoWithUrl(), prefixes: [SpecsPrefix]);

        var write = await sut.RunStepAsync(
            Write(".agentsmith/specs/a.yaml"), null, CancellationToken.None);

        write.ExitCode.Should().Be(0);
        factory.Spawned[0].Sandbox.RanSteps
            .Should().Contain(step => step.Kind == StepKind.WriteFile);
    }

    [Fact]
    public async Task SourceScope_AWriteOutsideEveryPrefix_IsRefused()
    {
        var factory = new StubSandboxFactory();
        var sut = Build(factory, RepoWithUrl(), prefixes: [SpecsPrefix]);

        var write = await sut.RunStepAsync(Write("src/Program.cs"), null, CancellationToken.None);

        write.ExitCode.Should().NotBe(0);
        write.ErrorMessage.Should().Contain(SpecsPrefix);
        factory.Spawned.Should().BeEmpty();
    }

    [Fact]
    public async Task SourceScope_AWriteEscapingThePolicyByTraversal_IsRefused()
    {
        var factory = new StubSandboxFactory();
        var sut = Build(factory, RepoWithUrl(), prefixes: [SpecsPrefix]);

        var write = await sut.RunStepAsync(
            Write($"{SpecsPrefix}/../../../etc/cron.d/task"), null, CancellationToken.None);

        write.ExitCode.Should().NotBe(0);
        write.ErrorMessage.Should().Contain("/etc/cron.d/task", "the path is collapsed before it is matched");
        factory.Spawned.Should().BeEmpty();
    }

    [Fact]
    public async Task SourceScope_AnAbsoluteWriteOutsideTheWorkRoot_IsRefused()
    {
        var factory = new StubSandboxFactory();
        var sut = Build(factory, RepoWithUrl(), prefixes: [SpecsPrefix]);

        var write = await sut.RunStepAsync(Write("/etc/passwd"), null, CancellationToken.None);

        write.ExitCode.Should().NotBe(0);
        write.ErrorMessage.Should().Contain("outside the checkout");
        factory.Spawned.Should().BeEmpty();
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
        // 2026-09-13-9802: the clone is followed by a rev-parse that reports the sha.
        kinds.Should().Equal(StepKind.Run, StepKind.Run, StepKind.ReadFile, StepKind.Grep);
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

    private static SourceScopeSandbox Build(
        StubSandboxFactory factory, RepoConnection repo, string? revision = null,
        IReadOnlyCollection<string>? prefixes = null)
    {
        var specBuilder = new SandboxSpecBuilder(
            new StubSandboxResourceResolver(),
            Mock.Of<Application.Services.Sandbox.IAgentImageResolver>(
                r => r.Resolve(It.IsAny<ResolvedProject>()) == "agent:test"));
        var runContext = new Mock<IRunContextAccessor>();
        runContext.SetupGet(r => r.CurrentRunId).Returns("run-1");
        return new SourceScopeSandbox(
            Project, repo, revision, conversationId: null,
            new SourceScopeOpener(new SourceScopeMaterialiser(), factory, specBuilder, runContext.Object),
            new AsyncLocalSourceScopeObserverAccessor(), NullLogger<SourceScopeSandbox>.Instance,
            prefixes);
    }

    [Fact]
    public async Task SourceScope_TheCloneAndTheCheckout_StillMaterialiseTheTree()
    {
        var factory = new StubSandboxFactory();
        var sut = Build(factory, RepoWithUrl(), revision: "v2.1.0");

        await sut.RunStepAsync(Step(StepKind.ReadFile), null, CancellationToken.None);

        var git = factory.Spawned[0].Sandbox.RanSteps
            .Where(s => s.Kind == StepKind.Run)
            .Select(s => string.Join(' ', s.Args ?? []))
            .ToList();
        git.Should().Contain(a => a.Contains("clone"), "the clone writes the whole tree");
        git.Should().Contain(a => a.Contains("checkout") && a.Contains("v2.1.0"));
    }

    [Fact]
    public async Task RunStepAsync_NoRevisionGiven_NeverChecksOut()
    {
        var factory = new StubSandboxFactory();
        var sut = Build(factory, RepoWithUrl());

        await sut.RunStepAsync(Step(StepKind.ReadFile), null, CancellationToken.None);

        factory.Spawned[0].Sandbox.RanSteps
            .Where(s => s.Kind == StepKind.Run)
            .Select(s => string.Join(' ', s.Args ?? []))
            .Should().NotContain(a => a.Contains("checkout"));
    }

    [Fact]
    public async Task MaterializeAsync_RepoWithoutUrl_ThrowsTypedSoACallerCanRefuse()
    {
        var sut = Build(new StubSandboxFactory(),
            new RepoConnection { Name = "local-only", Type = RepoType.Local });

        var act = () => sut.MaterializeAsync(CancellationToken.None);

        (await act.Should().ThrowAsync<SourceScopeUnavailableException>())
            .Which.Kind.Should().Be(SourceScopeFailureKind.NoCloneUrl);
    }

    private static RepoConnection RepoWithUrl() => new()
    {
        Name = "repo-a", Type = RepoType.GitHub, Url = "https://stub.test/repo-a",
    };

    /// <summary>The specifications root a later phase declares; here only a prefix to test with.</summary>
    private const string SpecsPrefix = ".agentsmith/specs";

    private static Step Step(StepKind kind) => new(
        AgentSmith.Sandbox.Wire.Step.CurrentSchemaVersion, Guid.NewGuid(), kind,
        Command: kind == StepKind.Run ? "echo" : null,
        Path: kind is StepKind.ReadFile or StepKind.Grep ? "/work/file.cs" : null,
        Pattern: kind == StepKind.Grep ? "x" : null,
        Content: kind == StepKind.WriteFile ? "content" : null);

    private static Step Process(string program, params string[] args) => new(
        AgentSmith.Sandbox.Wire.Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
        Command: program, Args: args);

    private static Step Write(string path) => new(
        AgentSmith.Sandbox.Wire.Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteFile,
        Path: path, Content: "content");
}
