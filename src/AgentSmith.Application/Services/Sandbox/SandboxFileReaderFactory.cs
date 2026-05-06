using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Builds an ISandboxFileReader bound to a specific sandbox instance.
/// Singleton in DI; the bound reader is per-pipeline ephemeral.
/// </summary>
public sealed class SandboxFileReaderFactory : ISandboxFileReaderFactory
{
    public ISandboxFileReader Create(ISandbox sandbox) => new SandboxFileReader(sandbox);
}
