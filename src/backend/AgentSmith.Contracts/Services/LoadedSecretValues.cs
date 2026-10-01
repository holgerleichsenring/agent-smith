using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-01-7f7aa: the secret values of ONE loaded configuration, fixed for its lifetime.
/// The CLI's answer — its configuration is a file read once per process, so there is nothing
/// to follow — and the snapshot <c>CurrentSecretValues</c> replaces whenever the server's
/// config store changes. Names match under <see cref="ConfigNames.Comparer"/>, the rule every
/// catalog reference is resolved by.
/// </summary>
public sealed class LoadedSecretValues : ISecretValues
{
    private readonly Dictionary<string, string> _byName = new(ConfigNames.Comparer);
    private readonly IReadOnlyList<string> _all;

    public LoadedSecretValues(AgentSmithConfig config)
    {
        foreach (var (name, value) in config.Secrets) _byName.TryAdd(name, value);
        _all = config.Secrets.Values
            .Concat(config.Registries.Select(r => r.Token))
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();
    }

    public string? Resolve(string name) =>
        _byName.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    public IReadOnlyList<string> All() => _all;
}
