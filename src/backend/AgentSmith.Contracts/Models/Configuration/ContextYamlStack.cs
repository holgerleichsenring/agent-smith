namespace AgentSmith.Contracts.Models.Configuration;

/// <param name="Lang">`stack.lang:` — canonical language slug (fallback image selection).</param>
/// <param name="Image">p0265: `stack.image:` — the exact toolchain Docker image. Named by the
/// analyzer/context-generator LLM; wins over the language→image convention table.</param>
/// <param name="Resources">p0268: `stack.resources:` — LLM-authored k8s CPU/memory request+limit
/// for this stack's sandbox; sizes the container as a layer between the operator project
/// override and the global default. Null = use the project/global default.</param>
/// <remarks>
/// 2026-08-26-04b6: what is left is exactly what the reader has ever read. `runtime`,
/// `frameworks`, `sdks`, `infra` and `testing` were READINGS — every one of them is stated by
/// a build file, a manifest or a project reference still in the tree, so the copy here could
/// only ever disagree with the original and nothing could tell which was true. The schema
/// still accepts them; the writer no longer emits them.
/// </remarks>
public sealed record ContextYamlStack(
    string? Lang = null,
    string? Image = null,
    ContextYamlStackResources? Resources = null);
