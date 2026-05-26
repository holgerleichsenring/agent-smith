using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Resolves the patterns directory.
///
/// Priority:
///   1. AGENTSMITH_CONFIG_DIR env var (operator override for custom patterns)
///   2. Skill catalog root pulled from agentsmith-skills (cacheDir/patterns)
///   3. Local config/patterns (development convenience when running from repo root)
/// </summary>
public sealed class PatternsDirectoryResolver(ISkillsCatalogPath catalogPath)
{
    public string Resolve()
    {
        var configDir = Environment.GetEnvironmentVariable("AGENTSMITH_CONFIG_DIR");
        if (!string.IsNullOrEmpty(configDir))
        {
            var direct = Path.Combine(configDir, "patterns");
            if (Directory.Exists(direct))
                return direct;

            var nested = Path.Combine(configDir, "config", "patterns");
            if (Directory.Exists(nested))
                return nested;
        }

        try
        {
            var catalogPatterns = Path.Combine(catalogPath.Root, "patterns");
            if (Directory.Exists(catalogPatterns))
                return catalogPatterns;
        }
        catch (InvalidOperationException)
        {
            // Catalog not yet resolved (e.g. CLI tooling running before bootstrap).
        }

        var fallbacks = new[]
        {
            Path.Combine("config", "patterns"),
            Path.Combine(AppContext.BaseDirectory, "config", "patterns"),
        };
        return fallbacks.FirstOrDefault(Directory.Exists) ?? fallbacks[0];
    }
}
