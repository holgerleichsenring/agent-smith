namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0268: the LLM-authored `stack.resources:` block — raw, UNPARSED Kubernetes
/// quantities for one stack's sandbox. All four fields are nullable on purpose:
/// the block is all-or-none, and the SandboxResourceResolver is the single gate
/// that validates (parse-as-quantity) and either maps the whole block to a
/// <see cref="Sandbox.ResourceLimits"/> or rejects it WHOLE and falls back to the
/// next layer with a WARN. Keeping the fields raw here (rather than a mapped
/// ResourceLimits with baked-in defaults) is what preserves the "partial → reject"
/// signal — a default-filled record would silently complete a partial block.
/// </summary>
/// <param name="CpuRequest">`cpu_request:` — k8s CPU quantity, e.g. "500m".</param>
/// <param name="CpuLimit">`cpu_limit:` — k8s CPU quantity, e.g. "2".</param>
/// <param name="MemoryRequest">`memory_request:` — k8s memory quantity, e.g. "1Gi".</param>
/// <param name="MemoryLimit">`memory_limit:` — k8s memory quantity, e.g. "4Gi".</param>
public sealed record ContextYamlStackResources(
    string? CpuRequest = null,
    string? CpuLimit = null,
    string? MemoryRequest = null,
    string? MemoryLimit = null);
