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

    /// <summary>2026-11-09-9631a: the spawn from this count on fails, as a full namespace does.</summary>
    public int? FailsFrom { get; set; }

    public Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        if (Spawned.Count >= FailsFrom) throw new InvalidOperationException("no capacity left");
        var sandbox = new FakeHoldableSandbox();
        Spawned.Add((spec, sandbox));
        return Task.FromResult<ISandbox>(sandbox);
    }
}
