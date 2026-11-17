using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Config;

/// <summary>
/// 2026-10-01-7f7aa: the server's secret values, following the config store. 2026-11-17-274ca: the
/// store-following load is <see cref="CurrentConfigSnapshot"/>'s, shared with the loop limits; this
/// re-reads the secrets map only when the snapshot hands back a new configuration instance, because
/// the masker asks on every trace line.
/// </summary>
public sealed class CurrentSecretValues(CurrentConfigSnapshot snapshot) : ISecretValues
{
    private readonly object _gate = new();
    private AgentSmithConfig? _seen;
    private LoadedSecretValues _values = new(AgentSmithConfig.Empty());

    public string? Resolve(string name) => Current().Resolve(name);

    public IReadOnlyList<string> All() => Current().All();

    private LoadedSecretValues Current()
    {
        var config = snapshot.Value;
        lock (_gate)
        {
            if (ReferenceEquals(config, _seen)) return _values;
            _values = new LoadedSecretValues(config);
            _seen = config;
            return _values;
        }
    }
}
