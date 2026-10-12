using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>p0196: hands out fresh StubSandbox instances; tracks every spawn.</summary>
internal sealed class StubSandboxFactory : ISandboxFactory
{
    public List<(SandboxSpec Spec, StubSandbox Sandbox)> Spawned { get; } = new();

    /// <summary>2026-10-02-3f06c: repo-relative paths every sandbox handed out answers "file not
    /// found" for — empty by default, so every read keeps succeeding as it always has.</summary>
    public HashSet<string> MissingPaths { get; } = new(StringComparer.Ordinal);

    /// <summary>2026-10-12-50bda: when set, every sandbox names its spec's image the way a container
    /// backend does, so the run records SandboxImages and the image preflight checks judge it.</summary>
    public bool NamesImages { get; set; }

    public Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        var sandbox = new StubSandbox(MissingPaths, NamesImages ? spec.ToolchainImage : null);
        Spawned.Add((spec, sandbox));
        return Task.FromResult<ISandbox>(sandbox);
    }
}
