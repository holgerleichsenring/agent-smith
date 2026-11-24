using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933dc: the pocs block a run reads at its step — the configuration as it stands, so a settings edit
/// applies to the next scan; the defaults when there is no configuration to read.</summary>
public sealed class PocsConfigSource(IConfigurationLoader? loader = null, ServerContext? server = null)
{
    public PocsConfig Current() => All().Pocs;

    /// <summary>2026-11-23-933dd: the whole configuration as it stands — the install resolves the target's source repo in it.</summary>
    public AgentSmithConfig All() =>
        loader is not null && server is not null ? loader.LoadConfig(server.ConfigPath) : new AgentSmithConfig();
}
