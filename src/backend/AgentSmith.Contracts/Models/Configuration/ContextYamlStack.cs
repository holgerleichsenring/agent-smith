namespace AgentSmith.Contracts.Models.Configuration;

/// <param name="Lang">`stack.lang:` — canonical language slug (fallback image selection).</param>
/// <param name="Image">p0265: `stack.image:` — the exact toolchain Docker image. Named by the
/// analyzer/context-generator LLM; wins over the language→image convention table.</param>
public sealed record ContextYamlStack(
    string? Lang = null,
    string? Image = null,
    string? Runtime = null,
    IReadOnlyList<string>? Infra = null,
    IReadOnlyList<string>? Testing = null,
    IReadOnlyList<string>? Frameworks = null,
    IReadOnlyList<string>? Sdks = null);
