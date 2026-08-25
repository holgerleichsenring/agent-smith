namespace AgentSmith.Contracts.Models.Configuration;

/// <param name="Workdir">Sub-tree relative to repo root. "." for single-stack. REQUIRED per p0161.</param>
/// <param name="Type">`meta.type:` — one to four system archetypes (agent, pipeline, api, …).
/// 2026-08-25-056d: a LIST, because the schema has always declared it as one and both shipped
/// context files write one. As a single string the writer emitted YAML its own schema rejected,
/// and a document repeating the two archetypes this repo declares failed to deserialise at all.</param>
/// <param name="Domain">p0504: one word naming a profile in the skills catalog. A context
/// declaring a domain need not name a stack.image — the profile brings one.</param>
public sealed record ContextYamlMeta(
    string Workdir,
    string? Project = null,
    string? Version = null,
    IReadOnlyList<string>? Type = null,
    string? Purpose = null,
    string? Domain = null);
