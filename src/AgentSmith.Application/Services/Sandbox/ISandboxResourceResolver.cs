using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Resolves the effective <see cref="ResourceLimits"/> for a sandbox toolchain
/// container by walking the override chain: projects.&lt;name&gt;.sandbox.resources
/// (per-project) wins; otherwise the global SandboxOptions defaults apply.
/// </summary>
public interface ISandboxResourceResolver
{
    ResourceLimits Resolve(ResolvedProject projectConfig);
}
