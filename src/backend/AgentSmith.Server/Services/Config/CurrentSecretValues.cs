using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Config;

/// <summary>
/// 2026-10-01-7f7aa: the server's secret values, following the config store. The store
/// replaces its catalog instance on every reload — its own write, or an epoch advance seen on
/// any replica — so a changed instance is the signal to re-read the secrets map through the
/// same loader a run reads its configuration with. An unchanged instance re-reads nothing:
/// the masker asks on every trace line.
/// </summary>
public sealed class CurrentSecretValues(
    IConfigStore store, IConfigurationLoader loader, ServerContext context) : ISecretValues
{
    private readonly object _gate = new();
    private ConfigCatalog? _seen;
    private LoadedSecretValues _values = new(AgentSmithConfig.Empty());

    public string? Resolve(string name) => Current().Resolve(name);

    public IReadOnlyList<string> All() => Current().All();

    private LoadedSecretValues Current()
    {
        var catalog = store.Catalog;
        lock (_gate)
        {
            if (ReferenceEquals(catalog, _seen)) return _values;
            _values = new LoadedSecretValues(loader.LoadConfig(context.ConfigPath));
            _seen = catalog;
            return _values;
        }
    }
}
