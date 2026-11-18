namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-18-9ae8: the one rule a credential named by an environment variable resolves by — the variable when it
/// holds a value, otherwise the catalog secret of that name (case-insensitive, <see cref="Models.Configuration.ConfigNames"/>),
/// whose value is its own reference's variable or the value stored in the admin area. 2026-11-18-78efb: a stored
/// value that exists but cannot be opened is its own answer, not an absent one.
/// </summary>
public static class EnvironmentFirst
{
    public static CredentialLookup Lookup(string name, Func<string, string?> environment, ISecretValues catalog)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(catalog);
        if (string.IsNullOrWhiteSpace(name)) return CredentialLookup.None;
        if (environment(name) is { Length: > 0 } fromEnvironment) return CredentialLookup.Of(fromEnvironment);
        if (catalog.Resolve(name) is { } stored) return CredentialLookup.Of(stored);
        return catalog.Unreadable(name) is { } reason ? CredentialLookup.Unreadable(name, reason) : CredentialLookup.None;
    }

    /// <summary>The value, or null when there is none — unreadable included.</summary>
    public static string? Resolve(string name, Func<string, string?> environment, ISecretValues catalog) =>
        Lookup(name, environment, catalog).Value;
}
