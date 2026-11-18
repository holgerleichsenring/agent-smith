namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-18-9ae8: the one rule a credential named by an environment variable resolves by — the variable when it
/// holds a value, otherwise the catalog secret of that name (case-insensitive, <see cref="Models.Configuration.ConfigNames"/>),
/// whose value is its own reference's variable or the value stored in the admin area. Null when neither has one.
/// </summary>
public static class EnvironmentFirst
{
    public static string? Resolve(string name, Func<string, string?> environment, ISecretValues catalog)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(catalog);
        if (string.IsNullOrWhiteSpace(name)) return null;
        var fromEnvironment = environment(name);
        return string.IsNullOrEmpty(fromEnvironment) ? catalog.Resolve(name) : fromEnvironment;
    }
}
