using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Judges an LLM-authored <c>context.yaml stack.resources</c> block: accepted WHOLE and
/// clamped to the operator's ceiling, or rejected whole so the caller falls through to the
/// next layer. 2026-09-22-6c46 carved it out of <see cref="SandboxResourceResolver"/>,
/// which sat at its file-length baseline and so could not take the layer accessor the
/// project sandbox tab needs while it also owned the validation and the clamp.
/// </summary>
public interface IContextResourceAcceptance
{
    /// <summary>The block as the sandbox may use it, or null when it is partial, does not
    /// parse as Kubernetes quantities, or was not authored at all.</summary>
    ResourceLimits? Accept(ContextYamlStackResources? contextResources);
}
