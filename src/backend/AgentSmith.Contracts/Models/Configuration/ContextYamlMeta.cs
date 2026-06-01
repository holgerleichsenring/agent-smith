namespace AgentSmith.Contracts.Models.Configuration;

/// <param name="Workdir">Sub-tree relative to repo root. "." for single-stack. REQUIRED per p0161.</param>
public sealed record ContextYamlMeta(
    string Workdir,
    string? Project = null,
    string? Version = null,
    string? Type = null,
    string? Purpose = null);
