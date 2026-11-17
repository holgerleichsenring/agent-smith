using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Config;

/// <summary>
/// 2026-11-17-274ca: the server's configuration as the store holds it NOW. The store replaces its
/// catalog instance on every reload — its own write, or an epoch advance seen on any replica — so a
/// changed instance is the signal to load again through the loader a run reads with. An unchanged
/// instance loads nothing, so a reader that asks on every trace line pays one reference compare.
/// The secrets and the loop limits read it, so a Studio change reaches them without a restart.
/// An unreadable store is one more catalog state: the loader degrades to the empty configuration
/// and records why, exactly as the AgentSmithConfig it replaces does, and loads again once the
/// store can be read.
/// </summary>
public sealed class CurrentConfigSnapshot(IConfigStore store, IConfigurationLoader loader, ServerContext context)
{
    private readonly object _gate = new();
    private ConfigCatalog? _seen;
    private AgentSmithConfig _config = AgentSmithConfig.Empty();

    private static readonly ConfigCatalog Unreadable = new();

    public AgentSmithConfig Value
    {
        get
        {
            var catalog = ReadCatalog();
            lock (_gate)
            {
                if (ReferenceEquals(catalog, _seen)) return _config;
                _config = loader.LoadConfig(context.ConfigPath);
                _seen = catalog;
                return _config;
            }
        }
    }

    private ConfigCatalog ReadCatalog()
    {
        try
        {
            return store.Catalog;
        }
        catch (Exception)
        {
            return Unreadable;
        }
    }
}
