using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-2d11b: hands out holdable sandboxes and records every spawn — which is how a
/// second turn proves it spawned nothing.
/// </summary>
internal sealed class FakeSandboxSpawner : ISandboxFactory
{
    public List<(SandboxSpec Spec, FakeHoldableSandbox Sandbox)> Spawned { get; } = [];

    /// <summary>The one sandbox this spawner handed out; throws when it handed out more.</summary>
    public FakeHoldableSandbox Only => Spawned.Single().Sandbox;

    public Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        var sandbox = new FakeHoldableSandbox();
        Spawned.Add((spec, sandbox));
        return Task.FromResult<ISandbox>(sandbox);
    }
}
