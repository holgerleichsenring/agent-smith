using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-11-25-9992b: a run's workspace of stub containers — one <see cref="StubSandbox"/> per toolchain image, built
/// through <see cref="StubSandboxFactory"/> so its <see cref="StubSandboxFactory.Spawned"/> lists every container.
/// Each container serves every repository of the run, each at /work/&lt;repo&gt; with a tree of its own.
/// </summary>
internal sealed class StubWorkspaceFactory(StubSandboxFactory sandboxes) : IWorkspaceFactory
{
    public List<StubWorkspace> Created { get; } = [];

    public StubSandboxFactory Sandboxes => sandboxes;

    public Task<IWorkspace> CreateAsync(WorkspaceSpec spec, CancellationToken cancellationToken)
    {
        var containers = spec.Toolchains.Select(t => sandboxes.CreateContainer(
            spec.Base with { ToolchainImage = t.Image, Resources = t.Resources })).ToList();
        var workspace = new StubWorkspace(spec, [.. spec.Toolchains.Select(t => t.Image)], containers);
        Created.Add(workspace);
        return Task.FromResult<IWorkspace>(workspace);
    }
}
