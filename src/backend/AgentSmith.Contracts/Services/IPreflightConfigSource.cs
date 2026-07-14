using AgentSmith.Contracts.Models.Preflight;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0324: deferred, cached access to the loaded agentsmith.yml for preflight checks.
/// Keeps a parse failure inside the config-schema check (which reports it with a fix
/// hint) instead of blowing up dependency construction for every other check.
/// </summary>
public interface IPreflightConfigSource
{
    PreflightConfigResult Resolve();
}
