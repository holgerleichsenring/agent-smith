using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>p0196: hands out fresh StubSandbox instances; tracks every spawn.</summary>
internal sealed class StubSandboxFactory : ISandboxFactory
{
    public List<(SandboxSpec Spec, StubSandbox Sandbox)> Spawned { get; } = new();

    public Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        var sandbox = new StubSandbox();
        Spawned.Add((spec, sandbox));
        return Task.FromResult<ISandbox>(sandbox);
    }
}
