using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>2026-11-25-9992b: a restart replaces the workspace between unwatching and watching again, restores every
/// repository into the new one, and ends the run when anything fails once the old one is gone.</summary>
public sealed class WorkspaceRestartTests
{
    private const string Image = "mcr.microsoft.com/dotnet/sdk:10.0";
    private readonly StubWorkspaceFactory _stubs = new(new StubSandboxFactory());
    private readonly List<string> _order = [];

    private (PipelineContext Pipeline, RunWorkspace Workspace) Run(IWorkspaceFactory factory)
    {
        var workspace = new RunWorkspace(factory);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [new RepoConnection { Name = "api", Url = "https://host/api.git" }]);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, new Dictionary<string, ISandbox>
            { ["api"] = new RepoSandbox(new WorkspaceContainerSandbox(workspace, Image), "api") });
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, new Dictionary<string, string> { ["api"] = "api" });
        return (pipeline, workspace);
    }

    private static WorkspaceSpec Spec(params string[] images) =>
        new(new SandboxSpec(images[0], new ResourceLimits()), [.. images.Select(i => new WorkspaceToolchain(i, new ResourceLimits()))]);

    private ISandboxLivenessSupervisor Liveness()
    {
        var liveness = new Mock<ISandboxLivenessSupervisor>();
        liveness.Setup(l => l.UnwatchAllAsync()).Callback(() => _order.Add($"unwatch@{_stubs.Created.Count}")).Returns(Task.CompletedTask);
        return liveness.Object;
    }

    [Fact]
    public async Task RestartAsync_ReplacesBetweenUnwatchAndWatch_AndRestoresTheRepositoryInTheNewWorkspace()
    {
        var (pipeline, workspace) = Run(_stubs);
        await workspace.CreateAsync(Spec(Image), CancellationToken.None);

        await TestCoordinators.Restart().RestartAsync(pipeline, workspace, Spec(Image, "node:20"), Liveness(),
            () => _order.Add($"watch@{_stubs.Created.Count}"), CancellationToken.None);

        _order.Should().Equal("unwatch@1", "watch@2");
        _stubs.Created[0].DisposeCount.Should().Be(1);
        _stubs.Created[1].Containers.SelectMany(c => c.RanSteps)
            .Should().Contain(s => s.Args != null && s.Args.Contains("-B") && s.WorkingDirectory == "/work/api");
    }

    [Fact]
    public async Task RestartAsync_NewWorkspaceCannotBeCreated_EndsTheRunAsAVanishedSandbox()
    {
        var failing = new FailingSecondFactory(_stubs);
        var (pipeline, workspace) = Run(failing);
        await workspace.CreateAsync(Spec(Image), CancellationToken.None);
        var cancellation = new Mock<IRunCancellationRegistry>();

        var act = () => TestCoordinators.Restart(cancellation.Object).RestartAsync(
            pipeline, workspace, Spec(Image, "node:20"), Liveness(), () => _order.Add("watch"), CancellationToken.None);

        (await act.Should().ThrowAsync<WorkspaceRestoreException>()).WithMessage("*did not come back*quota*");
        cancellation.Verify(c => c.TryCancel("run-1", "sandbox-vanished", It.Is<string>(d => d.Contains("quota"))), Times.Once);
        _order.Should().NotContain("watch");
    }

    private sealed class FailingSecondFactory(IWorkspaceFactory inner) : IWorkspaceFactory
    {
        private int _calls;

        public Task<IWorkspace> CreateAsync(WorkspaceSpec spec, CancellationToken cancellationToken) =>
            ++_calls == 1 ? inner.CreateAsync(spec, cancellationToken)
                : throw new InvalidOperationException("exceeded quota: sandbox-quota");
    }
}
