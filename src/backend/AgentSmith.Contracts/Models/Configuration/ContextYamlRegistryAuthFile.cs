namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// One templated auth-config file inside a context.yaml <c>registry_auth</c>
/// section (p0375). <see cref="Content"/> carries <c>__AS_TOKEN_&lt;host&gt;__</c>
/// placeholders ONLY — a real secret never lands in the repo; the host
/// substitutes the matched registry token just before writing into the sandbox.
/// </summary>
public sealed record ContextYamlRegistryAuthFile(string Path, string Content);
