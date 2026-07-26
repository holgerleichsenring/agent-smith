namespace AgentSmith.Application.Models.Registry;

/// <summary>
/// A single global auth-config file emitted by the LLM stager or declared in a
/// context.yaml <c>registry_auth</c> section. <see cref="Content"/> still
/// carries <c>__AS_TOKEN_&lt;host&gt;__</c> placeholders — the real token is
/// substituted host-side by <c>RegistryTokenSubstitutor</c> just before writing.
/// </summary>
public sealed record StagedAuthFile(string Path, string Content);
