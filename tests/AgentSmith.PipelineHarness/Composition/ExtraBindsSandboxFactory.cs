using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199b: decorates the inner ISandboxFactory's spec with the per-test
/// session's bare-repo bind mount. The DockerSandboxFactory uses the
/// ExtraBinds list to add host-side bind mounts to the toolchain
/// container, so the in-sandbox `git clone file:///bare-remotes/...`
/// succeeds without a network. Tracks every spawned sandbox so the test
/// can assert "container existed during run, removed at end".
/// 2026-10-25-c5a7b: a hardened spec is decorated to the root profile, because the host-owned
/// bare repository is not writable by uid 1000.
/// 2026-11-25-9992b: a run's workspace is decorated the same way — its shared base spec carries the bind into every
/// toolchain container — and each of its containers is tracked as a spawned sandbox.
/// </summary>
public sealed class ExtraBindsSandboxFactory(
    ISandboxFactory inner, IWorkspaceFactory innerWorkspaces, DockerHarnessSession session)
    : ISandboxFactory, IWorkspaceFactory
{
    public List<ISandbox> Spawned { get; } = new();

    public async Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        var sandbox = await inner.CreateAsync(Decorate(spec), cancellationToken);
        Spawned.Add(sandbox);
        return sandbox;
    }

    public async Task<IWorkspace> CreateAsync(WorkspaceSpec spec, CancellationToken cancellationToken)
    {
        var workspace = await innerWorkspaces.CreateAsync(spec with { Base = Decorate(spec.Base) }, cancellationToken);
        Spawned.AddRange(workspace.Images.Select(workspace.Container));
        return workspace;
    }

    private SandboxSpec Decorate(SandboxSpec spec) => spec with
    {
        ExtraBinds = (spec.ExtraBinds ?? Array.Empty<string>()).Append(session.ExtraBind).ToList(),
        SecurityContext = spec.SecurityContext is { RunAsRoot: false } ? SandboxSecurityContext.Root : spec.SecurityContext,
    };
}
