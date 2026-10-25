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
/// </summary>
public sealed class ExtraBindsSandboxFactory(
    ISandboxFactory inner, DockerHarnessSession session) : ISandboxFactory
{
    public List<ISandbox> Spawned { get; } = new();

    public async Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        var binds = (spec.ExtraBinds ?? Array.Empty<string>()).Append(session.ExtraBind).ToList();
        var decorated = spec with
        {
            ExtraBinds = binds,
            SecurityContext = spec.SecurityContext is { RunAsRoot: false } ? SandboxSecurityContext.Root : spec.SecurityContext,
        };
        var sandbox = await inner.CreateAsync(decorated, cancellationToken);
        Spawned.Add(sandbox);
        return sandbox;
    }
}
