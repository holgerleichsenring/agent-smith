using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: the pocs.archive bounds a run reads at its step — the configuration as it stands, so a
/// settings edit applies to the next intake; the defaults when there is no configuration to read.</summary>
public sealed class PocArchiveLimitsSource(IConfigurationLoader? loader = null, ServerContext? server = null)
{
    public PocArchiveLimits Current() =>
        loader is not null && server is not null ? loader.LoadConfig(server.ConfigPath).Pocs.Archive : new PocArchiveLimits();
}
