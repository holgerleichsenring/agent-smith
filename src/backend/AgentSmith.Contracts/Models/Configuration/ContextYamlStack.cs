namespace AgentSmith.Contracts.Models.Configuration;

/// <param name="Lang">`stack.lang:` — drives sandbox toolchain image selection.</param>
public sealed record ContextYamlStack(
    string? Lang = null,
    string? Runtime = null,
    IReadOnlyList<string>? Infra = null,
    IReadOnlyList<string>? Testing = null,
    IReadOnlyList<string>? Frameworks = null,
    IReadOnlyList<string>? Sdks = null);
