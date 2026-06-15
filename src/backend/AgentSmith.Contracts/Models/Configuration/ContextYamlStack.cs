namespace AgentSmith.Contracts.Models.Configuration;

/// <param name="Lang">`stack.lang:` — canonical language slug (fallback image selection).</param>
/// <param name="Image">p0265: `stack.image:` — the exact toolchain Docker image. Named by the
/// analyzer/context-generator LLM; wins over the language→image convention table.</param>
/// <param name="Resources">p0268: `stack.resources:` — LLM-authored k8s CPU/memory request+limit
/// for this stack's sandbox; sizes the container as a layer between the operator project
/// override and the global default. Null = use the project/global default.</param>
public sealed record ContextYamlStack(
    string? Lang = null,
    string? Image = null,
    string? Runtime = null,
    IReadOnlyList<string>? Infra = null,
    IReadOnlyList<string>? Testing = null,
    IReadOnlyList<string>? Frameworks = null,
    IReadOnlyList<string>? Sdks = null,
    ContextYamlStackResources? Resources = null);
