namespace AgentSmith.Application.Models.Registry;

/// <summary>
/// A single global auth-config file the LLM fallback emitted. <see cref="Content"/>
/// still carries __AS_TOKEN_&lt;host&gt;__ placeholders — the real token is
/// substituted host-side by <c>RegistryTokenSubstitutor</c> just before writing.
/// </summary>
public sealed record StagedAuthFile(string Path, string Content);
