namespace AgentSmith.Contracts.Models.Configuration;

/// <param name="Lang">`stack.lang:` — canonical language slug (fallback image selection).</param>
/// <param name="Frameworks">`stack.frameworks:` — application frameworks this stack is built on.
/// Emitted since p0193; declared in context.schema.json only in 2026-08-25-056d, so until then
/// a context that named its frameworks failed the schema its own writer emits.</param>
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
