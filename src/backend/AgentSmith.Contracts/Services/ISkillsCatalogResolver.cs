using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Idempotent entry point for materialising the skill catalog. Safe to call
/// multiple times — first invocation runs the source handler, subsequent calls
/// are no-ops.
/// </summary>
public interface ISkillsCatalogResolver
{
    Task EnsureResolvedAsync(SkillsConfig config, CancellationToken cancellationToken);
}
