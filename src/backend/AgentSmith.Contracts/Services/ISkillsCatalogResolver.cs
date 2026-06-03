using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Entry point for materialising the skill catalog. Safe to call multiple times:
/// it delegates to the (idempotent, version-aware) source handler on every call
/// and returns the binding this call resolved to — so a changed pin or a wiped
/// cache self-heals, and each caller learns whether it hit the warm cache.
/// </summary>
public interface ISkillsCatalogResolver
{
    Task<CatalogResolution> EnsureResolvedAsync(SkillsConfig config, CancellationToken cancellationToken);
}
