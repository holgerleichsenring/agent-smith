namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0506: resolves a <c>${NAME}</c> reference wherever the configuration allows one —
/// the secrets map, registry tokens and a project's jira_trigger.secret. An unresolved
/// reference is EMPTY, never the literal placeholder: a value no sender can ever match
/// would read as "a secret is configured" and refuse every delivery.
/// </summary>
public sealed class ConfigSecretReferences(Func<string, string?> envReader)
{
    /// <summary>A reference resolves against the environment variable it names.</summary>
    public string ResolveFromEnvironment(string value) =>
        Referenced(value) is { } name ? envReader(name) ?? string.Empty : value;

    /// <summary>
    /// 2026-10-02-5f89a: the raw value of one environment variable, for the legacy-credential
    /// migration — the one reader of the fixed credential variables left.
    /// </summary>
    public string? Environment(string name) => envReader(name);

    /// <summary>
    /// A reference resolves against the already-materialized secrets map first — the
    /// registry-token convention — then against the environment variable it names.
    /// </summary>
    public string Resolve(string value, IReadOnlyDictionary<string, string> secrets) =>
        Referenced(value) is { } name
            ? secrets.TryGetValue(name, out var fromSecrets) ? fromSecrets : envReader(name) ?? string.Empty
            : value;

    private static string? Referenced(string value) =>
        value.StartsWith("${", StringComparison.Ordinal) && value.EndsWith('}')
            ? value[2..^1]
            : null;
}
