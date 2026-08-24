namespace AgentSmith.Contracts.Models.Configuration;

/// <param name="Workdir">Sub-tree relative to repo root. "." for single-stack. REQUIRED per p0161.</param>
/// <param name="Domain">p0504: one word naming a profile in the skills catalog. A context
/// declaring a domain need not name a stack.image — the profile brings one.</param>
public sealed record ContextYamlMeta(
    string Workdir,
    string? Project = null,
    string? Version = null,
    string? Type = null,
    string? Purpose = null,
    string? Domain = null);
