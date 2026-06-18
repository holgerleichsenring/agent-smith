using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Resolves the effective <see cref="ResourceLimits"/> for a sandbox toolchain
/// container by walking the override chain (p0268):
/// projects.&lt;name&gt;.sandbox.resources (operator) ?? context.yaml stack.resources
/// (LLM, validated) ?? the global SandboxOptions default. A partial or
/// parse-invalid context block is rejected WHOLE and falls through with a WARN —
/// never silently, never to the project layer.
/// </summary>
public interface ISandboxResourceResolver
{
    ResourceLimits Resolve(ResolvedProject projectConfig, ContextYamlStackResources? contextResources = null);
}
