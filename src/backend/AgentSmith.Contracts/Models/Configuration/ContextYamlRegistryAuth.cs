namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// The context.yaml <c>registry_auth</c> section (p0375): the persisted /
/// operator-declared template for staging private-registry auth files in the
/// sandbox. A present section is authoritative — the LLM stager is skipped and
/// the template is replayed (placeholder substitution + write) on every run.
/// The per-host association lives in the placeholders themselves: each
/// <c>__AS_TOKEN_&lt;host&gt;__</c> occurrence names the registry host whose
/// token is substituted host-side.
/// </summary>
public sealed record ContextYamlRegistryAuth(
    IReadOnlyList<ContextYamlRegistryAuthFile> Files);
