using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Configuration.Resolved;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Resolves the effective <see cref="ResourceLimits"/> for a sandbox toolchain
/// container by walking the override chain (p0268):
/// projects.&lt;name&gt;.sandbox.resources (operator) ?? context.yaml stack.resources
/// (LLM, validated + clamped) ?? the global SandboxOptions default. A partial or
/// parse-invalid context block is rejected WHOLE and falls through with a WARN —
/// never silently, never to the project layer. p0320a: resolution is
/// pipeline-aware — only code-changing pipelines consume the LLM-authored build
/// sizing; non-code-changing pipelines (and a null/unknown pipeline) get the
/// fixed <see cref="ResourceLimits.LightProfile"/> unless the operator override applies.
/// </summary>
public interface ISandboxResourceResolver
{
    ResourceLimits Resolve(
        ResolvedProject projectConfig, string? pipelineName,
        ContextYamlStackResources? contextResources = null);

    /// <summary>
    /// 2026-09-22-6c46: which layer produced what <see cref="Resolve"/> returns for the
    /// same inputs. The layer is NOT recoverable from the value — the light profile and a
    /// configured global default can hold identical numbers — and the project sandbox tab
    /// has to name the layer it would inherit from rather than pretend to one number.
    /// </summary>
    SandboxResourceLayer ResolveLayer(
        ResolvedProject projectConfig, string? pipelineName,
        ContextYamlStackResources? contextResources = null);
}
